using Microsoft.Data.SqlClient;

namespace DeepDive;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Contains("--demo", StringComparer.OrdinalIgnoreCase))
        {
            Application.Run(new MainForm(new SessionStore(),
                "Demo mode. Changes last until you close Deep Dive."));
            return;
        }

        string? configuredName = Environment.GetEnvironmentVariable("DEEPDIVE_DB_NAME")?.Trim();
        string databaseName = string.IsNullOrEmpty(configuredName) ? "DeepDive" : configuredName;
        var localStore = new LocalDbStore(new DbConnector(databaseName));

        IStore store = localStore;
        string? sessionNotice = null;
        try
        {
            string[] requiredCodes = ["APP", "FOLDER", "WEBSITE"];
            var codes = localStore.GetTypes().Select(ItemRules.Code).ToHashSet();
            if (!requiredCodes.All(codes.Contains))
                throw new InvalidOperationException("The import must include APP, FOLDER, and WEBSITE action types.");
            localStore.GetSpaces();
            localStore.CheckResultTable();
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or InvalidCastException
            or System.ComponentModel.Win32Exception)
        {
            store = new SessionStore();
            sessionNotice = $"Demo mode. LocalDB database '{databaseName}' is unavailable. Changes last until you close Deep Dive.";
            MessageBox.Show(
                $"Deep Dive will open in demo mode. To save changes across sessions, import the database into your Windows account’s LocalDB instance and reopen Deep Dive.\n\n{exception.Message}",
                "LocalDB unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        Application.Run(new MainForm(store, sessionNotice));
    }
}
