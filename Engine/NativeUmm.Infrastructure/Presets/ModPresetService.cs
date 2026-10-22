using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using NativeUmm.Domain.Mods;

namespace NativeUmm.Infrastructure.Presets;

public sealed class ModPresetService
{
    public bool[] Load(ModInfo mod, string kind)
    {
        var (path, keys) = Resolve(mod, kind);
        if (!File.Exists(path)) return [true, true, false];
        if (kind == "tweaks")
        {
            var document = ReadXml(path);
            return keys.Select(key => document.Root!.Element(key)?.Value == "true").ToArray();
        }
        var json = ReadJson(path);
        return keys.Select(key => json[key]?.GetValue<bool>() ?? (key == "EnableRainEffect")).ToArray();
    }

    public void Save(ModInfo mod, string kind, bool[] values)
    {
        if (values.Length != 3) throw new InvalidDataException("Exactly three preset values are required.");
        var (path, keys) = Resolve(mod, kind);
        var original = File.Exists(path) ? File.ReadAllBytes(path) : null;
        string content;
        if (kind == "tweaks")
        {
            var document = original is null ? new XDocument(new XElement("DisableEffectsSettings")) : ReadXml(path);
            document.Root!.SetElementValue("IsEnabled", "true");
            for (var i = 0; i < keys.Length; i++) document.Root.SetElementValue(keys[i], values[i] ? "true" : "false");
            content = document.ToString();
        }
        else
        {
            if (original is null) throw new InvalidDataException("Launch the game once to create this mod's settings.");
            var json = ReadJson(path);
            for (var i = 0; i < keys.Length; i++) json[keys[i]] = values[i];
            content = json.ToJsonString();
        }
        if (original is not null)
        {
            var backup = Contained(mod.Path, Path.GetRelativePath(mod.Path, path) + ".amm-backup");
            if (!File.Exists(backup)) AtomicWrite(backup, original);
            if (!File.ReadAllBytes(path).SequenceEqual(original)) throw new IOException("Settings changed. Reload them before saving.");
        }
        AtomicWrite(path, System.Text.Encoding.UTF8.GetBytes(content));
    }

    private static (string Path, string[] Keys) Resolve(ModInfo mod, string kind)
    {
        if (!mod.Installed) throw new InvalidDataException("Install the mod before changing its settings.");
        if (kind == "tweaks" && mod.Id.Equals("AdofaiTweaks", StringComparison.OrdinalIgnoreCase) && mod.Version == "2.9.3")
            return (Contained(mod.Path, "DisableEffectsSettings.xml"), ["DisableScreenShake", "DisableFlash", "DisableBloom"]);
        if (kind != "keyViewer" || !mod.Id.Equals("JipperKeyViewer", StringComparison.OrdinalIgnoreCase) || mod.Version != "1.7.2")
            throw new InvalidDataException("This mod version does not support these presets.");
        var metadata = ReadJson(Contained(mod.Path, "config/settings.json"));
        var profile = metadata["CurrentProfile"]?.GetValue<string>();
        if (metadata["Version"]?.GetValue<int>() != 6 || string.IsNullOrWhiteSpace(profile) || profile is "." or ".." || profile.IndexOfAny(['/', '\\', '\0', ':']) >= 0)
            throw new InvalidDataException("Unsupported Key Viewer profile. Launch the game once, then try again.");
        var path = Contained(mod.Path, "config/profiles/" + profile + ".json");
        if (!File.Exists(path)) throw new InvalidDataException("Launch the game once to create this mod's settings.");
        return (path, ["EnableRainEffect", "EnablePressAnimation", "EnablePerKeyKps"]);
    }

    private static string Contained(string root, string relative)
    {
        root = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Settings path is outside the mod directory.");
        // Refuse links at every level, including the mod directory and backup target.
        var current = root;
        CheckLink(current);
        foreach (var part in Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar))
        { current = Path.Combine(current, part); CheckLink(current); }
        return path;
    }

    private static void CheckLink(string path)
    {
        var info = new FileInfo(path);
        if (info.LinkTarget is not null || (File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Linked settings paths are not supported.");
    }

    private static JsonObject ReadJson(string path) => JsonNode.Parse(File.ReadAllText(path)) as JsonObject
        ?? throw new InvalidDataException("Invalid mod settings.");

    private static XDocument ReadXml(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var document = XDocument.Load(reader);
        if (document.Root?.Name != "DisableEffectsSettings") throw new InvalidDataException("Invalid Tweaks settings.");
        return document;
    }

    private static void AtomicWrite(string path, byte[] data)
    {
        var temp = path + ".amm-" + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, data); File.Move(temp, path, overwrite: true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
