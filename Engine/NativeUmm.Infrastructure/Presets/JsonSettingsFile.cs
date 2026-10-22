using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using NativeUmm.Domain.Mods;

namespace NativeUmm.Infrastructure.Presets;

internal static class JsonSettingsFile
{
    public static JsonObject Read(ModInfo mod, string fileName, bool migrateXml = false) => Load(mod, fileName, migrateXml).Json;

    public static void Update(ModInfo mod, string fileName, Action<JsonObject> change, bool migrateXml = false)
    {
        var (path, original, json) = Load(mod, fileName, migrateXml);
        change(json);
        var content = Encoding.UTF8.GetBytes(json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        if (original is not null && original.SequenceEqual(content)) return;
        var backup = path + ".amm-backup";
        CheckPath(backup);
        if (original is not null)
        {
            if (!File.Exists(backup)) Write(backup, original, false);
            if (!File.ReadAllBytes(path).SequenceEqual(original)) throw new IOException("Settings changed. Reload them before saving.");
        }
        else if (File.Exists(path)) throw new IOException("Settings changed. Reload them before saving.");
        CheckPath(path);
        Write(path, content, original is not null);
    }

    private static (string Path, byte[]? Original, JsonObject Json) Load(ModInfo mod, string fileName, bool migrateXml)
    {
        var path = Path.Combine(Path.GetFullPath(mod.Path), fileName);
        CheckPath(path);
        if (!Directory.Exists(mod.Path)) throw new DirectoryNotFoundException(mod.Path);
        byte[]? original = null;
        if (File.Exists(path))
        {
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Mod settings are too large.");
            original = File.ReadAllBytes(path);
        }
        var json = original is not null
            ? JsonNode.Parse(Encoding.UTF8.GetString(original).TrimStart('\uFEFF')) as JsonObject
                ?? throw new InvalidDataException("Invalid mod settings JSON.")
            : migrateXml ? ReadLegacyChatter(mod) : new JsonObject();
        return (path, original, json);
    }

    private static JsonObject ReadLegacyChatter(ModInfo mod)
    {
        var path = Path.Combine(mod.Path, "Setting.xml");
        CheckPath(path);
        if (!File.Exists(path)) return new JsonObject();
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 });
        var root = XDocument.Load(reader).Root;
        if (root?.Name.LocalName != "Setting") throw new InvalidDataException("Invalid KeyboardChatterBlocker XML settings.");
        var json = new JsonObject();
        foreach (var element in root.Elements())
        {
            var name = element.Name.LocalName;
            if (name == "keyLimiterProfiles")
            {
                var profiles = new JsonArray();
                foreach (var profile in element.Elements())
                {
                    var item = new JsonObject();
                    foreach (var value in profile.Elements()) item[value.Name.LocalName] = LegacyValue(value);
                    profiles.Add((JsonNode)item);
                }
                json[name] = profiles;
            }
            else json[name] = LegacyValue(element);
        }
        // Retain original XML; the mod prefers JSON on its next load.
        return json;
    }

    private static JsonNode? LegacyValue(XElement element)
    {
        if (element.HasElements || element.Name.LocalName is "allowedKeys" or "allowedAsyncKeys" or "ignoredKeys" or "ignoredAsyncKeys")
            return new JsonArray(element.Elements().Select(LegacyValue).ToArray());
        if (bool.TryParse(element.Value, out var flag)) return JsonValue.Create(flag);
        if (int.TryParse(element.Value, out var number)) return JsonValue.Create(number);
        return JsonValue.Create(element.Value);
    }

    private static void CheckPath(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            var info = new FileInfo(current);
            if (info.LinkTarget is not null ||
                (File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked settings paths are not supported.");
        }
    }

    private static void Write(string path, byte[] bytes, bool overwrite)
    {
        var temp = path + ".amm-" + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, bytes); File.Move(temp, path, overwrite); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
