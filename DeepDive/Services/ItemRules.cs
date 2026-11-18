namespace DeepDive;

internal static class ItemRules
{
    internal static string Code(ItemType type) => type.Code.Trim().ToUpperInvariant();

    internal static bool IsSupported(ItemType type) => Code(type) is "APP" or "FOLDER" or "WEBSITE";

    internal static string? Validate(Item item)
    {
        if (item.Name.Trim().Length is 0 or > 100)
            return "Enter an item name between 1 and 100 characters.";

        string target = item.Target.Trim();
        if (target.Length is 0 or > 1000)
            return "Enter a path or web address between 1 and 1,000 characters.";

        return Code(item.Type) switch
        {
            "APP" when !IsLocalPath(target) ||
                !string.Equals(Path.GetExtension(target), ".exe", StringComparison.OrdinalIgnoreCase)
                => "Choose a full path to a local .exe file.",
            "FOLDER" when !IsLocalPath(target) => "Choose a full path to a local folder.",
            "WEBSITE" when !Uri.TryCreate(target, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrEmpty(uri.Host)
                => "Enter a complete web address starting with http:// or https://.",
            "APP" or "FOLDER" or "WEBSITE" => null,
            _ => "Choose Application, Folder, or Website."
        };
    }

    private static bool IsLocalPath(string path) =>
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' &&
        path[2] is '\\' or '/' && path.IndexOfAny(Path.GetInvalidPathChars()) < 0 &&
        path.AsSpan(2).IndexOfAny("<>|*?:") < 0;
}
