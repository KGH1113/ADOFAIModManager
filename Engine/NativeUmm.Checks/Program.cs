using System.Text.Json.Nodes;
using System.Xml.Linq;
using NativeUmm.Domain.Mods;
using NativeUmm.Infrastructure.Presets;
using NativeUmm.Infrastructure.Games;

var root = Path.Combine(OperatingSystem.IsMacOS() ? "/private/tmp" : Path.GetTempPath(), "amm-presets-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var service = new ModPresetService();
var checks = 0;
void Check(bool condition, string description) { if (!condition) throw new Exception(description); checks++; }
void Reject(Action action, string description) {
    try { action(); } catch (Exception error) when (error is InvalidDataException or System.Xml.XmlException) { checks++; return; }
    throw new Exception(description);
}
ModInfo Mod(string id, string version) {
    var path = Path.Combine(root, id);
    Directory.CreateDirectory(path);
    return new(id, id, version, null, null, "installed", path, true, true, []);
}
try {
    var steamapps = Path.Combine(root, "steamapps");
    var steamGame = Path.Combine(steamapps, "common", "A Dance of Fire and Ice");
    Directory.CreateDirectory(steamGame);
    var manifestPath = Path.Combine(steamapps, "appmanifest_977950.acf");
    string Manifest(string selected = "alpha", string mounted = "alpha", string flags = "4", string target = "123") =>
        "\"AppState\" { \"appid\" \"977950\" \"installdir\" \"A Dance of Fire and Ice\" " +
        $"\"StateFlags\" \"{flags}\" \"buildid\" \"123\" \"TargetBuildID\" \"{target}\" " +
        $"\"UserConfig\" {{ \"BetaKey\" \"{selected}\" }} \"MountedConfig\" {{ \"BetaKey\" \"{mounted}\" }} }}";
    File.WriteAllText(manifestPath, Manifest());
    Check(SteamBranchReader.Read(steamGame) == new SteamBranchStatus("alpha", true), "Detect completed alpha in the selected Steam library");
    File.WriteAllText(manifestPath, Manifest(mounted: "public"));
    Check(!SteamBranchReader.Read(steamGame).AlphaInstalled, "Selected alpha is not proof of installed alpha");
    File.WriteAllText(manifestPath, Manifest(flags: "1026"));
    Check(!SteamBranchReader.Read(steamGame).AlphaInstalled, "Updating alpha must not skip the guide");
    File.WriteAllText(manifestPath, Manifest(target: "456"));
    Check(!SteamBranchReader.Read(steamGame).AlphaInstalled, "Pending target build must not skip the guide");
    File.WriteAllText(manifestPath, Manifest(selected: "public"));
    Check(!SteamBranchReader.Read(steamGame).AlphaInstalled, "Switching away from alpha must show the guide");
    File.WriteAllText(manifestPath, Manifest().Replace("\"MountedConfig\"", "\"PrivateDepots\""));
    Check(!SteamBranchReader.Read(steamGame).AlphaInstalled, "Alpha in unrelated metadata must not match the mounted branch");
    File.WriteAllText(manifestPath, Manifest().Replace("977950", "123456"));
    Check(SteamBranchReader.Read(steamGame).Branch is null, "Reject another game's manifest");
    File.WriteAllText(manifestPath, Manifest());
    Check(SteamBranchReader.Read(Path.Combine(steamapps, "common", "Another Game")).Branch is null, "Reject a manifest for another installation directory");
    File.WriteAllText(manifestPath, Manifest()[..^1]);
    Check(SteamBranchReader.Read(steamGame).Branch is null, "Malformed manifests must fail conservatively");
    Check(SteamBranchReader.Read(root).Branch is null, "Non-Steam games remain unknown");
    File.Delete(manifestPath);
    Check(SteamBranchReader.Read(steamGame).Branch is null, "Missing manifests remain unknown");
    var linux = new NativeUmm.Cli.Platform.Linux.LinuxGameLocator();
    var linuxRoot = Path.Combine(root, "linux-game");
    var linuxManaged = Path.Combine(linuxRoot, "ADanceOfFireAndIce_Data", "Managed");
    Directory.CreateDirectory(linuxManaged);
    File.WriteAllBytes(Path.Combine(linuxManaged, "UnityEngine.CoreModule.dll"), []);
    File.WriteAllBytes(Path.Combine(linuxManaged, "Assembly-CSharp.dll"), []);
    var elf = new byte[64];
    elf[0] = 0x7f; elf[1] = (byte)'E'; elf[2] = (byte)'L'; elf[3] = (byte)'F'; elf[5] = 1; elf[18] = 62;
    var linuxExecutable = Path.Combine(linuxRoot, "ADanceOfFireAndIce.x86_64");
    File.WriteAllBytes(linuxExecutable, elf);
    var linuxGame = linux.Locate(linuxExecutable);
    Check(linuxGame is not null && linuxGame.GameRoot == linuxRoot, "Locate Linux game from executable");
    Check(linux.Read(linuxGame!) == "x64", "Read Linux ELF architecture");
    Check(linux.Locate(linuxManaged) is not null, "Locate Linux game from managed folder");
    Check(linux.Locate(Path.Combine(root, "missing-linux-game")) is null, "Explicit missing Linux path must not fall back to Steam");
    elf[18] = 183;
    File.WriteAllBytes(linuxExecutable, elf);
    Check(linux.Read(linuxGame!) == "arm64", "Read Linux ARM64 architecture");
    var protonRoot = Path.Combine(root, "proton-game");
    var protonManaged = Path.Combine(protonRoot, "A Dance of Fire and Ice_Data", "Managed");
    Directory.CreateDirectory(protonManaged);
    File.WriteAllBytes(Path.Combine(protonManaged, "UnityEngine.CoreModule.dll"), []);
    File.WriteAllBytes(Path.Combine(protonManaged, "Assembly-CSharp.dll"), []);
    var pe = new byte[70]; pe[0] = (byte)'M'; pe[1] = (byte)'Z'; pe[0x3c] = 64;
    pe[64] = (byte)'P'; pe[65] = (byte)'E'; pe[68] = 0x64; pe[69] = 0x86;
    File.WriteAllBytes(Path.Combine(protonRoot, "A Dance of Fire and Ice.exe"), pe);
    var protonGame = linux.Locate(protonRoot);
    Check(protonGame is not null && linux.Read(protonGame) == "x64", "Locate Proton game and read PE architecture");
    var tweaks = Mod("AdofaiTweaks", "2.9.3");
    var xml = Path.Combine(tweaks.Path, "DisableEffectsSettings.xml");
    const string original = "<DisableEffectsSettings><CustomSetting>keep</CustomSetting><DisableFlash>false</DisableFlash></DisableEffectsSettings>";
    File.WriteAllText(xml, original);
    service.Save(tweaks, "tweaks", [true, true, false]);
    Check(service.Load(tweaks, "tweaks").SequenceEqual([true, true, false]), "Tweaks roundtrip");
    Check(XDocument.Load(xml).Root!.Element("CustomSetting")!.Value == "keep", "Preserve unknown XML");
    service.Save(tweaks, "tweaks", [false, false, true]);
    Check(File.ReadAllText(xml + ".amm-backup") == original, "Preserve first backup across saves");
    var saved = File.ReadAllText(xml);
    Reject(() => service.Save(tweaks with { Version = "3.0.0" }, "tweaks", [true, true, true]), "Unknown version must fail");
    Check(File.ReadAllText(xml) == saved, "Unknown version must not overwrite settings");
    Reject(() => service.Save(tweaks, "tweaks", [true]), "Invalid preset shape must fail");
    File.WriteAllText(xml, "<!DOCTYPE x [<!ENTITY data SYSTEM 'file:///etc/passwd'>]><DisableEffectsSettings>&data;</DisableEffectsSettings>");
    Reject(() => service.Load(tweaks, "tweaks"), "XML external entities must fail");

    var viewer = Mod("JipperKeyViewer", "1.7.2");
    var config = Path.Combine(viewer.Path, "config");
    Directory.CreateDirectory(Path.Combine(config, "profiles"));
    var metadata = Path.Combine(config, "settings.json");
    var profile = Path.Combine(config, "profiles", "Default.json");
    File.WriteAllText(metadata, "{\"Version\":6,\"CurrentProfile\":\"Default\"}");
    File.WriteAllText(profile, "{\"Bindings\":[\"A\",\"S\"],\"CustomSetting\":42,\"EnablePerKeyKps\":true}");
    service.Save(viewer, "keyViewer", [true, true, false]);
    Check(service.Load(viewer, "keyViewer").SequenceEqual([true, true, false]), "Key Viewer roundtrip");
    var json = JsonNode.Parse(File.ReadAllText(profile))!;
    Check(json["CustomSetting"]!.GetValue<int>() == 42 && json["Bindings"]!.AsArray().Count == 2, "Preserve JSON and key bindings");
    File.WriteAllText(metadata, "{\"Version\":6,\"CurrentProfile\":\"../outside\"}");
    Reject(() => service.Load(viewer, "keyViewer"), "Profile traversal must fail");
    File.WriteAllText(metadata, "{\"Version\":7,\"CurrentProfile\":\"Default\"}");
    Reject(() => service.Load(viewer, "keyViewer"), "Unknown settings format must fail");
    if (!OperatingSystem.IsWindows()) {
        File.Delete(xml);
        File.CreateSymbolicLink(xml, profile);
        Reject(() => service.Save(tweaks, "tweaks", [true, true, true]), "Symlink settings must fail");
    }
    var onboarding = new OnboardingSetupService();
    var jrp = Mod("JipperResourcePack", "1.5.2.0");
    var chatter = Mod("KeyboardChatterBlocker", "0.1.2");
    var tuf = Mod("TUFHelper", "3.1.1");
    var jrpPath = Path.Combine(jrp.Path, "Settings.json");
    var tufPath = Path.Combine(tuf.Path, "Settings.json");
    onboarding.Apply([jrp, chatter], "jrp-key-sync");
    Check(JsonNode.Parse(File.ReadAllText(jrpPath))!["Feature"]!["KeyViewer"]!["Setting"]!["AutoSetupKeyLimit"]!.GetValue<bool>(), "Create JRP settings before first game launch");
    const string jrpOriginal = """{"Custom":"keep","Feature":{"KeyViewer":{"Enabled":false,"Setting":{"AutoSetupKeyLimit":false,"GhostKey16":[7,8],"key16":[9,10]}}}}""";
    File.WriteAllText(jrpPath, jrpOriginal);
    onboarding.Apply([jrp, chatter], "jrp-key-sync");
    var jrpJson = JsonNode.Parse(File.ReadAllText(jrpPath))!;
    Check(jrpJson["Feature"]!["KeyViewer"]!["Setting"]!["GhostKey16"]!.ToJsonString() == "[7,8]", "Preserve ghost keys");
    Check(jrpJson["Feature"]!["KeyViewer"]!["Setting"]!["key16"]!.ToJsonString() == "[9,10]", "Preserve key bindings");
    Check(!jrpJson["Feature"]!["KeyViewer"]!["Enabled"]!.GetValue<bool>() && jrpJson["Custom"]!.GetValue<string>() == "keep", "Preserve unrelated feature and custom settings");
    Check(File.ReadAllText(jrpPath + ".amm-backup") == jrpOriginal, "Back up JRP before applying onboarding settings");
    Reject(() => onboarding.Apply([jrp], "jrp-key-sync"), "Require KCB before enabling sync");
    Reject(() => onboarding.Apply([jrp with { Version = "2.0.0" }, chatter], "jrp-key-sync"), "Unknown JRP version must not change settings");
    onboarding.Apply([tuf], "tuf-overlay");
    Check(JsonNode.Parse(File.ReadAllText(tufPath))!["ShowTUFHelperOverlayer"]!.GetValue<bool>(), "Keep TUF's global overlay enabled before first game launch");
    File.WriteAllText(tufPath, """{"ShowTUFHelperOverlayer":true,"FavoriteLevels":[42],"LevelSaveFolder":"/custom/levels"}""");
    onboarding.Apply([tuf], "tuf-overlay");
    var tufJson = JsonNode.Parse(File.ReadAllText(tufPath))!;
    Check(tufJson["FavoriteLevels"]![0]!.GetValue<int>() == 42 && tufJson["LevelSaveFolder"]!.GetValue<string>() == "/custom/levels", "Preserve TUF favorites and paths");
    var before = File.ReadAllText(tufPath);
    onboarding.Apply([tuf], "tuf-overlay");
    Check(File.ReadAllText(tufPath) == before, "Idempotent onboarding setup");
    Reject(() => onboarding.Apply([tuf], "other"), "Reject unknown setup operations");
    File.WriteAllText(jrpPath, """{"Feature":[]}""");
    Reject(() => onboarding.Apply([jrp, chatter], "jrp-key-sync"), "Reject malformed settings instead of replacing them");
    Check(File.ReadAllText(jrpPath) == """{"Feature":[]}""", "Keep malformed settings intact");
    if (!OperatingSystem.IsWindows()) {
        File.Delete(jrpPath);
        File.CreateSymbolicLink(jrpPath, tufPath);
        Reject(() => onboarding.Apply([jrp, chatter], "jrp-key-sync"), "Refuse linked onboarding settings");
        Check(File.ReadAllText(tufPath) == before, "Leave symlink target unchanged");
    }
    ModDefaultsChecks.Run(root, Check, Reject);
    ModInstallationChecks.Run(root, Check);
    Console.WriteLine($"{checks} Linux/Proton discovery, architecture, preset, onboarding, preservation and path checks passed.");
} finally { Directory.Delete(root, recursive: true); }
