using System.ComponentModel;
using System.Diagnostics;

namespace DeepDive;

internal sealed class Launcher
{
    private readonly Action<ProcessStartInfo> _start;

    public Launcher() : this(StartProcess) { }

    internal Launcher(Action<ProcessStartInfo> start) => _start = start;

    public IReadOnlyList<Result> Launch(Space space)
    {
        DateTime time = DateTime.Now;
        return space.Items.Where(item => item.Enabled).OrderBy(item => item.Order)
            .Select(item => LaunchItem(space.Id, item, time)).ToList();
    }

    private Result LaunchItem(int spaceId, Item item, DateTime time)
    {
        Result Outcome(string code, string message, bool needsFix = false) => new()
        {
            SpaceId = spaceId, ItemId = item.Id, Time = time,
            Code = code, Message = message, NeedsFix = needsFix
        };

        if (ItemRules.Validate(item) is { } error)
            return Outcome("INVALID_TARGET", error, true);

        string target = item.Target.Trim();
        string type = ItemRules.Code(item.Type);
        if (type == "APP" && !File.Exists(target))
            return Outcome("NOT_FOUND", "Application file not found or inaccessible.", true);
        if (type == "FOLDER" && !Directory.Exists(target))
            return Outcome("NOT_FOUND", "Folder not found or inaccessible.", true);

        try
        {
            _start(new ProcessStartInfo
            {
                FileName = type == "WEBSITE" ? new Uri(target).AbsoluteUri : target,
                UseShellExecute = true,
                WorkingDirectory = type == "APP" ? Path.GetDirectoryName(target) ?? "" : ""
            });
            return type == "WEBSITE"
                ? Outcome("BROWSER_SENT", "Request sent to the default browser.")
                : Outcome("REQUEST_SENT", "Launch request sent.");
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException
            or UnauthorizedAccessException or ArgumentException)
        {
            string message = $"Windows could not open this item. {exception.Message}";
            return Outcome("LAUNCH_FAILED", message.Length > 255 ? message[..252] + "..." : message, true);
        }
    }

    private static void StartProcess(ProcessStartInfo info)
    {
        using Process? process = Process.Start(info);
    }
}
