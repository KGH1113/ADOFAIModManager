using System.IO.Compression;
using System.Text.Json.Nodes;
using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;
using NativeUmm.Infrastructure.Mods;

internal static class ModInstallationChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var gameRoot = Path.Combine(root, "recommended-installs");
        var managed = Path.Combine(gameRoot, "Managed");
        var layout = new GameInstallation(gameRoot, gameRoot, managed, Path.Combine(managed, "Assembly-CSharp.dll"),
            Path.Combine(managed, "UnityModManager"), Path.Combine(gameRoot, "Mods"), Path.Combine(root, "original.dll"));
        var appData = new TestAppData(root);
        var log = new TestLog();
        var mods = new FileModService(log, appData);
        void Install(string id, string version)
        {
            var zipPath = Path.Combine(root, id + ".zip");
            using (var zip = new ZipArchive(File.Create(zipPath), ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(zip.CreateEntry("Info.json").Open());
                writer.Write(new JsonObject { ["Id"] = id, ["DisplayName"] = id, ["Version"] = version }.ToJsonString());
            }
            mods.Install(layout, zipPath);
        }

        Install("JipperResourcePack", "1.5.2.0");
        var settingsPath = Path.Combine(layout.ModsPath, "JipperResourcePack", "Settings.json");
        var json = JsonNode.Parse(File.ReadAllText(settingsPath))!;
        check(json["Feature"]!["Status"]!["Setting"]!["ShowProgress"]!.GetValue<bool>(), "ZIP installation applies JRP defaults through FileModService");
        var viewer = json["Feature"]!["KeyViewer"]!["Setting"]!;
        viewer["KeyViewerStyle"] = 1;
        viewer["FootKeyViewerStyle"] = 2;
        viewer["key16"] = new JsonArray(97, 115, 100, 102, 103, 104, 106, 107, 108, 59, 113, 119, 101, 114, 116, 121);
        File.WriteAllText(settingsPath, json.ToJsonString());

        Install("KeyboardChatterBlocker", "0.1.2");
        var chatterPath = Path.Combine(layout.ModsPath, "KeyboardChatterBlocker", "Setting.json");
        var chatter = JsonNode.Parse(File.ReadAllText(chatterPath))!;
        check(chatter["inputInterval"]!.GetValue<int>() == 35 && chatter["enableKeyLimiter"]!.GetValue<bool>(), "ZIP installation configures chatter and syncs an existing JRP");
        var backup = File.ReadAllText(settingsPath + ".amm-backup");
        Install("JipperResourcePack", "1.5.2.0");
        json = JsonNode.Parse(File.ReadAllText(settingsPath))!;
        viewer = json["Feature"]!["KeyViewer"]!["Setting"]!;
        check(viewer["KeyViewerStyle"]!.GetValue<int>() == 1 && viewer["FootKeyViewerStyle"]!.GetValue<int>() == 2 && viewer["key16"]![0]!.GetValue<int>() == 97,
            "Updating a mod retains customized styles and key bindings");
        check(File.ReadAllText(settingsPath + ".amm-backup") == backup, "Mod updates retain the original settings backup");
        Install("TUFHelper", "3.1.1");
        var tuf = JsonNode.Parse(File.ReadAllText(Path.Combine(layout.ModsPath, "TUFHelper", "Settings.json")))!;
        check(tuf["IngameElementsSettings"]!["LevelInfo"]!["IsShowed"]!.GetValue<bool>() &&
            !tuf["IngameElementsSettings"]!["Combo"]!["IsShowed"]!.GetValue<bool>(), "ZIP installation configures TUF information and suppresses JRP duplicates");
        Install("EnhancedCountdown", "0.2.3");
        check(JsonNode.Parse(File.ReadAllText(Path.Combine(layout.ModsPath, "EnhancedCountdown", "UpdateSettings.json")))!["ReceiveBetaUpdates"]!.GetValue<bool>(),
            "ZIP installation enables beta updates in the current Countdown release");
        check(!log.Collect().Any(line => line.Level == "Warn"), "Fresh installation defaults complete without warnings");
    }

    private sealed class TestAppData(string root) : IAppDataPaths
    {
        public string Cache => Path.Combine(root, "cache");
        public string RemovedMods => Path.Combine(root, "removed-mods");
        public string RemovedUmm => Path.Combine(root, "removed-umm");
    }
    private sealed class TestLog : IOperationLog
    {
        private readonly List<OperationLogLine> lines = [];
        public void Begin() => lines.Clear();
        public void Info(string message) => lines.Add(new("Info", message));
        public void Warn(string message) => lines.Add(new("Warn", message));
        public void Fail(string message) => lines.Add(new("Fail", message));
        public IReadOnlyList<OperationLogLine> Collect() => lines;
    }
}
