using System.Globalization;
using System.Text.Json.Nodes;
using NativeUmm.Domain.Mods;

namespace NativeUmm.Infrastructure.Presets;

public sealed record JrpKeyBindings(int Style, Dictionary<int, int[]> Layouts);

/// <summary>Versioned installation defaults and explicit key layout edits. Other settings are preserved.</summary>
public sealed class OnboardingSetupService
{
    public static readonly int[] DefaultKey10 = [9, 49, 50, 101, 112, 61, 8, 92, 32, 44];
    private static readonly int[] DefaultKey12 = [9, 49, 50, 101, 112, 61, 8, 92, 32, 99, 44, 46];
    private static readonly int[] DefaultKey16 = [.. DefaultKey12, 301, 304, 13, 104];
    private static readonly int[] DefaultKey20 = [.. DefaultKey16, 301, 100, 303, 59];
    private static readonly int[] DefaultKey4 = [50, 101, 112, 61];
    private static readonly int[] DefaultKey8 = DefaultKey10[..8];

    public static string? DefaultKind(string id) => id.ToLowerInvariant() switch
    {
        "jipperresourcepack" => "jrp-defaults",
        "keyboardchatterblocker" => "chatter-defaults",
        "tufhelper" => "tuf-overlay",
        "enhancedcountdown" => "countdown-defaults",
        _ => null
    };

    public void Apply(IReadOnlyList<ModInfo> mods, string kind, string? systemLanguage = null)
    {
        switch (kind)
        {
            case "jrp-defaults":
                var jrp = Require(mods, "JipperResourcePack", "1.5.2.0", "1.5.3.0");
                JsonSettingsFile.Update(jrp, "Settings.json", json =>
                {
                    foreach (var name in new[] { "Status", "BPM", "Combo", "TimingScale", "Attempt", "KeyViewer" })
                        Feature(json, name)["Enabled"] = true;
                    var status = Setting(json, "Status");
                    foreach (var name in new[] { "ShowProgress", "ShowXAccuracy", "ShowMusicTime", "ShowProgressBar" })
                        status[name] = true;
                    status["ShowCheckpoint"] = false;
                    status["ShowBest"] = false;
                    status["TimeTextType"] = (systemLanguage ?? CultureInfo.CurrentUICulture.Name)
                        .StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                    Setting(json, "Combo")["EnableAutoCombo"] = false;
                    Setting(json, "Attempt")["ShowAttempt"] = true;
                    Setting(json, "Attempt")["ShowFullAttempt"] = true;
                    Feature(json, "ResourceChanger")["Enabled"] = false;
                    var changer = Setting(json, "ResourceChanger");
                    changer["ChangeRabbit"] = false;
                    changer["ChangeBallColor"] = false;
                    changer["ChangeTileColor"] = false;
                    var viewer = Setting(json, "KeyViewer");
                    viewer["useRain"] = true;
                    viewer["useGhostRain"] = true;
                    viewer["AutoSetupKeyLimit"] = true;
                    // Existing styles and personalized bindings survive imports and onboarding retries.
                    viewer.TryAdd("KeyViewerStyle", 3);
                    viewer.TryAdd("FootKeyViewerStyle", 0);
                    viewer.TryAdd("key10", Array(DefaultKey10));
                });
                Reconcile(mods, "JipperResourcePack");
                break;
            case "chatter-defaults":
                var chatter = Require(mods, "KeyboardChatterBlocker");
                JsonSettingsFile.Update(chatter, "Setting.json", json => json["inputInterval"] = 35, migrateXml: true);
                Reconcile(mods, "KeyboardChatterBlocker");
                break;
            case "jrp-key-sync":
                Require(mods, "KeyboardChatterBlocker");
                jrp = Require(mods, "JipperResourcePack", "1.5.2.0", "1.5.3.0");
                JsonSettingsFile.Update(jrp, "Settings.json", json => Setting(json, "KeyViewer")["AutoSetupKeyLimit"] = true);
                SynchronizeKeys(mods);
                break;
            case "tuf-overlay":
                ConfigureTuf(mods, defaults: true);
                break;
            case "countdown-defaults":
                var countdown = Require(mods, "EnhancedCountdown", "0.2.0", "0.2.1", "0.2.2", "0.2.3");
                JsonSettingsFile.Update(countdown, "UpdateSettings.json", json => json["ReceiveBetaUpdates"] = true);
                break;
            default:
                throw new InvalidDataException("Unknown onboarding setup.");
        }
    }

    public void Reconcile(IReadOnlyList<ModInfo> mods, string changedId)
    {
        if (changedId.Equals("JipperResourcePack", StringComparison.OrdinalIgnoreCase) ||
            changedId.Equals("KeyboardChatterBlocker", StringComparison.OrdinalIgnoreCase))
            SynchronizeKeys(mods);
        if (changedId.Equals("JipperResourcePack", StringComparison.OrdinalIgnoreCase) &&
            Find(mods, "TUFHelper") is { Version: "3.1.1" } && ActiveJrp(mods))
            ConfigureTuf(mods, defaults: false);
    }

    public JrpKeyBindings LoadKeys(IReadOnlyList<ModInfo> mods)
    {
        var jrp = Require(mods, "JipperResourcePack", "1.5.2.0", "1.5.3.0");
        var settings = Setting(JsonSettingsFile.Read(jrp, "Settings.json"), "KeyViewer");
        var style = ReadInt(settings, "KeyViewerStyle", 1) switch
        {
            0 => 12,
            1 => 16,
            2 => 16, // Key20 is no longer offered; keep its stored data until an explicit save.
            3 => 10,
            4 => 4,
            5 => 8,
            _ => throw new InvalidDataException("Unsupported JRP key viewer style.")
        };
        var layouts = new Dictionary<int, int[]>();
        foreach (var count in new[] { 4, 8, 10, 12, 16 })
        {
            var (_, name, defaults) = KeyLayout(count);
            var keys = ReadKeys(settings, name, defaults);
            if (keys.Length != count) throw new InvalidDataException($"Invalid Key{count} settings. Expected {count} keys.");
            layouts[count] = keys;
        }
        return new(style, layouts);
    }

    private static (int EnumValue, string Name, int[] Defaults) KeyLayout(int style) => style switch
    {
        4 => (4, "key4", DefaultKey4),
        8 => (5, "key8", DefaultKey8),
        10 => (3, "key10", DefaultKey10),
        12 => (0, "key12", DefaultKey12),
        16 => (1, "key16", DefaultKey16),
        _ => throw new InvalidDataException("Choose Key4, Key8, Key10, Key12, or Key16.")
    };

    public JrpKeyBindings SaveKeys(IReadOnlyList<ModInfo> mods, int[] keys, int style)
    {
        var (enumValue, name, _) = KeyLayout(style);
        if (keys.Length != style || keys.Any(k => k < 0 || k > 0x10fff))
            throw new InvalidDataException($"Provide {style} valid bindings.");
        var jrp = Require(mods, "JipperResourcePack", "1.5.2.0", "1.5.3.0");
        LoadKeys(mods);
        if (ActiveJrp(mods) && Find(mods, "KeyboardChatterBlocker") is { Enabled: true } chatter)
            ValidateChatterSettings(chatter);
        JsonSettingsFile.Update(jrp, "Settings.json", json =>
        {
            var viewer = Setting(json, "KeyViewer");
            viewer[name] = Array(keys);
            viewer["KeyViewerStyle"] = enumValue;
        });
        SynchronizeKeys(mods);
        return LoadKeys(mods);
    }

    private static void SynchronizeKeys(IReadOnlyList<ModInfo> mods)
    {
        if (!ActiveJrp(mods) || Find(mods, "KeyboardChatterBlocker") is not { Enabled: true } chatter)
            return;
        var jrp = Require(mods, "JipperResourcePack", "1.5.2.0", "1.5.3.0");
        var jrpJson = JsonSettingsFile.Read(jrp, "Settings.json");
        if (Feature(jrpJson, "KeyViewer")["Enabled"]?.GetValue<bool>() == false) return;
        var viewer = Setting(jrpJson, "KeyViewer");
        var keys = ReadInt(viewer, "KeyViewerStyle", 1) switch
        {
            0 => ReadKeys(viewer, "key12", DefaultKey12),
            1 => ReadKeys(viewer, "key16", DefaultKey16),
            2 => ReadKeys(viewer, "key20", DefaultKey20),
            3 => ReadKeys(viewer, "key10", DefaultKey10),
            4 => ReadKeys(viewer, "key4", DefaultKey4),
            5 => ReadKeys(viewer, "key8", DefaultKey8),
            _ => throw new InvalidDataException("Unsupported JRP key viewer style.")
        };
        int[] footKeys = ReadInt(viewer, "FootKeyViewerStyle", 2) switch
        {
            0 => [],
            1 => ReadKeys(viewer, "footkey2", [289, 284]),
            2 => ReadKeys(viewer, "footkey4", [289, 284, 288, 283]),
            3 => ReadKeys(viewer, "footkey6", [289, 284, 288, 283, 287, 282]),
            4 => ReadKeys(viewer, "footkey8", [289, 285, 288, 284, 287, 283, 286, 282]),
            5 => ReadKeys(viewer, "footkey16", [289, 285, 288, 284, 287, 283, 286, 282, 48, 54, 57, 53, 56, 52, 55, 51]),
            _ => throw new InvalidDataException("Unsupported JRP foot key viewer style.")
        };
        var activeKeys = keys.Concat(footKeys).Where(k => k > 0).Distinct().ToArray();
        if (activeKeys.Length == 0) throw new InvalidDataException("Configure JRP keys before enabling the key limiter.");
        // JRP populates platform-specific native codes during game initialization.
        ValidateChatterSettings(chatter);
        JsonSettingsFile.Update(jrp, "Settings.json", json => Setting(json, "KeyViewer")["AutoSetupKeyLimit"] = true);
        JsonSettingsFile.Update(chatter, "Setting.json", json =>
        {
            if (!json.ContainsKey("keyLimiterProfiles")) json["keyLimiterProfiles"] = new JsonArray();
            var profiles = json["keyLimiterProfiles"] as JsonArray ?? throw new InvalidDataException("Invalid key limiter profiles.");
            JsonObject? selected = null;
            foreach (var node in profiles)
            {
                var profile = node as JsonObject ?? throw new InvalidDataException("Invalid key limiter profile.");
                if (profile["name"]?.GetValue<string>() == "JipperResourcePack") selected ??= profile;
                profile["isSelected"] = false;
            }
            if (selected is null)
            {
                selected = new JsonObject { ["name"] = "JipperResourcePack" };
                profiles.Add((JsonNode)selected);
            }
            selected["isSelected"] = true;
            selected["allowedKeys"] = Array(activeKeys.Where(k => k < 0x1000));
            selected["allowedAsyncKeys"] = Array(activeKeys.Where(k => k >= 0x1000).Select(k => k - 0x1000));
            json["enableKeyLimiter"] = true;
        }, migrateXml: true);
    }

    private static void ValidateChatterSettings(ModInfo chatter)
    {
        var json = JsonSettingsFile.Read(chatter, "Setting.json", migrateXml: true);
        if (!json.ContainsKey("keyLimiterProfiles")) return;
        if (json["keyLimiterProfiles"] is not JsonArray profiles || profiles.Any(node => node is not JsonObject))
            throw new InvalidDataException("Invalid key limiter profiles.");
        foreach (var profile in profiles.OfType<JsonObject>())
            if (profile["name"] is not null &&
                (profile["name"] is not JsonValue name || !name.TryGetValue<string>(out _)))
                throw new InvalidDataException("Invalid key limiter profile name.");
    }

    private static void ConfigureTuf(IReadOnlyList<ModInfo> mods, bool defaults)
    {
        var tuf = Require(mods, "TUFHelper", "3.1.1");
        JsonSettingsFile.Update(tuf, "Settings.json", json =>
        {
            var elements = Object(json, "IngameElementsSettings");
            if (defaults)
            {
                json["ShowTUFHelperOverlayer"] = true;
                Element(elements, "Leaderboard", 5)["IsShowed"] = true;
                Element(elements, "LevelInfo", 2)["IsShowed"] = true;
                var score = Element(elements, "PPDisplayer", 3, 5);
                score["IsShowed"] = true;
                if (score["Categories"] is JsonObject categories && categories["PPDisplayer"] is JsonObject category)
                {
                    category["IsShowScore"] = true;
                    category["IsShowSpeed"] = true;
                }
            }
            if (!ActiveJrp(mods)) return;
            Element(elements, "Progress", 0, 2.5, -48)["IsShowed"] = false;
            Element(elements, "Combo", 1, 0, -125)["IsShowed"] = false;
            Element(elements, "Judgements", 7)["IsShowed"] = false;
            Element(elements, "CurrentBPM", 0, 2.5, -25.5)["IsShowed"] = false;
            Element(elements, "RealBPM", 0, 2.5, -2.5)["IsShowed"] = false;
        });
    }

    private static JsonObject Element(JsonObject elements, string id, int anchor, double x = 0, double y = 0)
    {
        if (!elements.ContainsKey(id))
            elements[id] = new JsonObject
            {
                ["Anchor"] = anchor, ["Position"] = new JsonObject { ["X"] = x, ["Y"] = y },
                ["Scale"] = 0.5, ["Categories"] = new JsonObject()
            };
        return elements[id] as JsonObject ?? throw new InvalidDataException($"Invalid TUF overlay element: {id}");
    }

    private static bool ActiveJrp(IReadOnlyList<ModInfo> mods) => Find(mods, "JipperResourcePack") is { Version: "1.5.2.0" or "1.5.3.0", Enabled: true };
    private static ModInfo? Find(IReadOnlyList<ModInfo> mods, string id) => mods.FirstOrDefault(m => m.Installed && m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    private static ModInfo Require(IReadOnlyList<ModInfo> mods, string id, params string[] versions)
    {
        var mod = Find(mods, id) ?? throw new InvalidDataException($"Install {id} before applying settings.");
        if (versions.Length > 0 && !versions.Contains(mod.Version)) throw new InvalidDataException($"Unsupported {id} settings version: {mod.Version}.");
        return mod;
    }
    private static JsonObject Feature(JsonObject json, string name) => Object(Object(json, "Feature"), name);
    private static JsonObject Setting(JsonObject json, string feature) => Object(Feature(json, feature), "Setting");
    private static JsonObject Object(JsonObject parent, string key)
    {
        if (!parent.ContainsKey(key)) parent[key] = new JsonObject();
        return parent[key] as JsonObject ?? throw new InvalidDataException($"Invalid settings object: {key}");
    }
    private static int ReadInt(JsonObject json, string key, int fallback) => json[key]?.GetValue<int>() ?? fallback;
    private static int[] ReadKeys(JsonObject json, string key, int[] fallback)
    {
        if (!json.ContainsKey(key)) return [.. fallback];
        var array = json[key] as JsonArray ?? throw new InvalidDataException($"Invalid key bindings: {key}");
        return array.Select(k => k?.GetValue<int>() ?? throw new InvalidDataException($"Invalid key binding: {key}")).ToArray();
    }
    private static JsonArray Array(IEnumerable<int> values) => new(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());
}
