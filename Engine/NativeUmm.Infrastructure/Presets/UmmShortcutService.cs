using System.Xml.Linq;

namespace NativeUmm.Infrastructure.Presets;

public sealed record UmmShortcut(string KeyCode, int Modifiers);

public sealed class UmmShortcutService
{
    public static readonly UmmShortcut Default = new("F10", 1);
    private static readonly HashSet<string> NamedKeys = [
        "Tab", "Space", "Return", "Backspace", "Delete", "Insert", "Home", "End",
        "PageUp", "PageDown", "UpArrow", "DownArrow", "LeftArrow", "RightArrow",
        "BackQuote", "Minus", "Equals", "LeftBracket", "RightBracket", "Semicolon",
        "Quote", "Backslash", "Comma", "Period", "Slash"
    ];

    private static bool ValidKey(string key) => NamedKeys.Contains(key) ||
        (key.Length == 1 && key[0] is >= 'A' and <= 'Z') ||
        (key.Length == 6 && key.StartsWith("Alpha") && key[5] is >= '0' and <= '9') ||
        (key.StartsWith('F') && int.TryParse(key.AsSpan(1), out var f) && f is >= 1 and <= 12);

    private static string SettingsPath(string managerPath)
    {
        var path = Path.Combine(managerPath, "Params.xml");
        if (new DirectoryInfo(managerPath).LinkTarget is not null || new FileInfo(path).LinkTarget is not null)
            throw new InvalidDataException("Linked UMM settings cannot be edited.");
        return path;
    }

    private static XDocument Read(string path)
    {
        var document = File.Exists(path) ? XDocument.Load(path, LoadOptions.PreserveWhitespace) : new XDocument(new XElement("Param"));
        if (document.Root?.Name != "Param") throw new InvalidDataException("UMM settings are invalid.");
        return document;
    }

    public UmmShortcut Load(string managerPath)
    {
        var hotkey = Read(SettingsPath(managerPath)).Root!.Element("Hotkey");
        var key = (string?)hotkey?.Element("keyCode");
        if (string.IsNullOrEmpty(key) || key == "None") return Default;
        var modifiers = (int?)hotkey?.Element("modifiers") ?? 0;
        if (modifiers is < 0 or > 7) throw new InvalidDataException("UMM shortcut modifiers are invalid.");
        return new(key, modifiers);
    }

    public UmmShortcut Save(string managerPath, string key, int modifiers)
    {
        if (!ValidKey(key) || modifiers is < 0 or > 7) throw new InvalidDataException("Unsupported UMM shortcut.");
        var path = SettingsPath(managerPath);
        var document = Read(path);
        var hotkey = document.Root!.Element("Hotkey");
        if (hotkey is null) { hotkey = new XElement("Hotkey"); document.Root.Add(hotkey); }
        hotkey.SetElementValue("keyCode", key);
        hotkey.SetElementValue("modifiers", modifiers);
        Directory.CreateDirectory(managerPath);
        var temporary = path + $".tmp-{Guid.NewGuid():N}";
        try
        {
            document.Save(temporary);
            if (File.Exists(path)) File.Copy(path, path + ".adofai-mod-manager.bak", overwrite: true);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return new(key, modifiers);
    }
}
