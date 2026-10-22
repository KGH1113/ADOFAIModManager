using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using dnlib.DotNet;
using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;
using NativeUmm.Domain.Installation;
using NativeUmm.Domain.Mods;
using NativeUmm.Infrastructure.Games;
using NativeUmm.Infrastructure.Presets;

namespace NativeUmm.Cli.Protocol;

internal sealed class EngineRequestHandler(
    IGameLocator gameLocator,
    IArchitectureReader architectureReader,
    IUmmInstaller installer,
    IModService modService,
    IOperationLog log,
    IGameProcessProbe processProbe,
    ModPresetService presets)
{
    public string Handle(string requestJson)
    {
        string? action;
        string? gamePath;
        string? payloadDir;
        string? zipPath;
        string? modPath;
        string? modId;
        var urls = new List<string>();
        bool forcePayload;
        bool? enabled;
        string? presetKind;
        bool[] presetValues;
        int[] keyBindings;
        int keyViewerStyle;

        using (var doc = JsonDocument.Parse(requestJson))
        {
            var root = doc.RootElement;
            action = root.TryGetProperty("action", out var a) ? a.GetString() : null;
            gamePath = root.TryGetProperty("gamePath", out var g) ? g.GetString() : null;
            payloadDir = root.TryGetProperty("payloadDir", out var p) ? p.GetString() : null;
            zipPath = root.TryGetProperty("zipPath", out var z) ? z.GetString() : null;
            modPath = root.TryGetProperty("path", out var pp) ? pp.GetString() : null;
            modId = root.TryGetProperty("modId", out var mid) ? mid.GetString() : null;
            forcePayload = root.TryGetProperty("forcePayload", out var f) && f.ValueKind == JsonValueKind.True;
            enabled = root.TryGetProperty("enabled", out var e) &&
                (e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.False)
                ? e.GetBoolean()
                : null;
            presetKind = root.TryGetProperty("presetKind", out var pk) ? pk.GetString() : null;
            presetValues = root.TryGetProperty("presetValues", out var pv) && pv.ValueKind == JsonValueKind.Array
                ? pv.EnumerateArray().Select(value => value.GetBoolean()).ToArray() : [];
            keyBindings = root.TryGetProperty("keyBindings", out var kb) && kb.ValueKind == JsonValueKind.Array
                ? kb.EnumerateArray().Select(value => value.GetInt32()).ToArray() : [];
            keyViewerStyle = root.TryGetProperty("keyViewerStyle", out var ks) && ks.ValueKind == JsonValueKind.Number
                ? ks.GetInt32() : 10;
            if (root.TryGetProperty("urls", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var item in arr.EnumerateArray())
                {
                    var u = item.GetString();
                    if (!string.IsNullOrWhiteSpace(u)) urls.Add(u);
                }
        }

        log.Begin();

        var layout = gameLocator.Locate(string.IsNullOrWhiteSpace(gamePath) ? null : gamePath);
        if (layout is null)
            return BuildResponse(null, null, null, null, detected: false,
                error: "A Dance of Fire and Ice was not found. Select the game application or folder.");

        var bundledVersion = BundledVersion(payloadDir);
        if (action is "install" or "remove" or "restore" or "installmod" or "installmods" or "setmodenabled" or "uninstallmod" or "restoremod" or "removemod" or "savepreset" or "onboardingsetup" or "savejrpkeys")
        {
            if (processProbe.IsRunning()) throw new InvalidOperationException("game_running");
        }

        switch (action)
        {
            case "status":
                break;
            case "install":
                installer.InstallAsync(layout, payloadDir, forcePayload).GetAwaiter().GetResult();
                break;
            case "remove":
                installer.RemoveHook(layout);
                break;
            case "restore":
                installer.RestoreOriginal(layout);
                break;
            case "mods":
                return BuildResponse(layout, installer.ReadStatus(layout), bundledVersion, modService.List(layout), detected: true, error: null);
            case "installmod":
                if (string.IsNullOrWhiteSpace(zipPath))
                    return BuildResponse(layout, installer.ReadStatus(layout), bundledVersion, modService.List(layout), detected: true, error: "No zip path provided.");
                modService.Install(layout, zipPath);
                return BuildResponse(layout, installer.ReadStatus(layout), bundledVersion, modService.List(layout), detected: true, error: null);
            case "inspectmod":
                if (string.IsNullOrWhiteSpace(zipPath))
                    return BuildResponse(layout, installer.ReadStatus(layout), bundledVersion, modService.List(layout), detected: true, error: "No zip path provided.");
                return BuildInspection(modService.Inspect(layout, zipPath));
            case "setmodenabled":
                if (string.IsNullOrWhiteSpace(modId) || enabled is null)
                    return BuildResponse(layout, installer.ReadStatus(layout), bundledVersion, modService.List(layout), detected: true, error: "No mod state provided.");
                modService.SetEnabled(layout, modId, enabled.Value);
                return BuildResponse(layout, installer.ReadStatus(layout), bundledVersion, modService.List(layout), detected: true, error: null);
            case "installmods":
                foreach (var url in urls)
                {
                    try { modService.InstallFromUrlAsync(layout, url).GetAwaiter().GetResult(); }
                    catch (Exception ex) { log.Fail($"Failed to install {url}: {ex.Message}"); }
                }
                return BuildResponse(layout, installer.ReadStatus(layout), bundledVersion, modService.List(layout), detected: true, error: null);
            case "uninstallmod":
                if (!string.IsNullOrWhiteSpace(modPath))
                    modService.Uninstall(layout, modPath);
                return BuildResponse(layout, installer.ReadStatus(layout), bundledVersion, modService.List(layout), detected: true, error: null);
            case "restoremod":
                if (!string.IsNullOrWhiteSpace(modPath))
                    modService.Restore(layout, modPath);
                return BuildResponse(layout, installer.ReadStatus(layout), bundledVersion, modService.List(layout), detected: true, error: null);
            case "removemod":
                if (!string.IsNullOrWhiteSpace(modPath))
                    modService.Remove(layout, modPath);
                return BuildResponse(layout, installer.ReadStatus(layout), bundledVersion, modService.List(layout), detected: true, error: null);
            case "logtail":
                return BuildLogTail(Path.Combine(layout.ManagerPath, "Log.txt"), 400);
            case "gamelog":
                var gameLogPath = OperatingSystem.IsMacOS()
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", "7th Beat Games", "A Dance of Fire and Ice", "Player.log")
                    : OperatingSystem.IsLinux()
                        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "unity3d", "7th Beat Games", "A Dance of Fire and Ice", "Player.log")
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "7th Beat Games", "A Dance of Fire and Ice", "Player.log");
                return BuildLogTail(gameLogPath, 30);
            case "onboardingsetup":
                new OnboardingSetupService().Apply(modService.List(layout), presetKind ?? "");
                return BuildResponse(layout, installer.ReadStatus(layout), bundledVersion, modService.List(layout), detected: true, error: null);
            case "jrpkeys":
            case "savejrpkeys":
                var setup = new OnboardingSetupService();
                var bindings = action == "savejrpkeys"
                    ? setup.SaveKeys(modService.List(layout), keyBindings, keyViewerStyle)
                    : setup.LoadKeys(modService.List(layout));
                return BuildKeyBindings(bindings);
            case "preset":
            case "savepreset":
                var mod = modService.List(layout).FirstOrDefault(m => m.Installed && m.Id.Equals(modId, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException("Install the mod before changing its settings.");
                if (action == "savepreset") presets.Save(mod, presetKind ?? "", presetValues);
                return BuildPreset(presets.Load(mod, presetKind ?? ""));
            default:
                return BuildResponse(layout, installer.ReadStatus(layout), bundledVersion, modService.List(layout), detected: true, error: $"Unknown action: {action}");
        }

        return BuildResponse(layout, installer.ReadStatus(layout), bundledVersion, modService.List(layout), detected: true, error: null);
    }

    private string BuildResponse(GameInstallation? layout, InstallStatus? status, string? bundledVersion,
        List<ModInfo>? mods, bool detected, string? error)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteBoolean("ok", error is null);
            w.WriteBoolean("detected", detected);
            w.WriteBoolean("gameRunning", processProbe.IsRunning());
            if (error is not null)
                w.WriteString("error", error);
            if (bundledVersion is not null)
                w.WriteString("bundledVersion", bundledVersion);

            w.WriteStartObject("status");
            if (layout is not null)
            {
                w.WriteString("gameRoot", layout.GameRoot);
                w.WriteString("appPath", layout.AppPath);
                w.WriteString("managedPath", layout.ManagedPath);
                w.WriteString("modsPath", layout.ModsPath);
                w.WriteString("processArch", RuntimeInformation.ProcessArchitecture.ToString());
                w.WriteString("gameArch", SafeArch(layout));
                var branch = SteamBranchReader.Read(layout.GameRoot);
                if (branch.Branch is not null) w.WriteString("steamBranch", branch.Branch);
                w.WriteBoolean("alphaInstalled", branch.AlphaInstalled);
            }
            if (status is not null)
            {
                w.WriteBoolean("hookInstalled", status.HookInstalled);
                w.WriteBoolean("managerInstalled", status.ManagerInstalled);
                if (status.ManagerVersion is not null)
                    w.WriteString("managerVersion", status.ManagerVersion);
                w.WriteBoolean("hasBackup", status.HasOriginalBackup);
                w.WriteString("backupPath", status.OriginalBackupPath);
                if (status.Warning is not null)
                    w.WriteString("warning", status.Warning);
            }
            w.WriteEndObject();

            if (mods is not null)
            {
                w.WriteStartArray("mods");
                foreach (var mod in mods)
                {
                    w.WriteStartObject();
                    w.WriteString("id", mod.Id);
                    w.WriteString("name", mod.DisplayName);
                    w.WriteString("version", mod.Version);
                    if (mod.ManagerVersion is not null)
                        w.WriteString("managerVersion", mod.ManagerVersion);
                    if (mod.HomePage is not null)
                        w.WriteString("homePage", mod.HomePage);
                    w.WriteString("path", mod.Path);
                    w.WriteString("status", mod.Status);
                    w.WriteBoolean("installed", mod.Installed);
                    w.WriteBoolean("enabled", mod.Enabled);
                    if (mod.Requirements.Count > 0)
                    {
                        w.WriteStartArray("requirements");
                        foreach (var req in mod.Requirements)
                        {
                            w.WriteStartObject();
                            w.WriteString("id", req.Id);
                            if (req.Version is not null)
                                w.WriteString("version", req.Version);
                            w.WriteString("state", req.State);
                            w.WriteEndObject();
                        }
                        w.WriteEndArray();
                    }
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }

            WriteLog(w);
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string BuildLogTail(string logPath, int lineCount)
    {
        var exists = File.Exists(logPath);
        var text = "";
        if (exists)
        {
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(Math.Max(0, stream.Length - 1024 * 1024), SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            if (stream.Position > 0) reader.ReadLine();
            text = string.Join("\n", reader.ReadToEnd().TrimEnd('\r', '\n').Split('\n').TakeLast(lineCount));
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteBoolean("ok", true);
            w.WriteBoolean("detected", true);
            w.WriteBoolean("logExists", exists);
            w.WriteString("logText", text);
            w.WriteString("logPath", logPath);
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string BuildPreset(bool[] values)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("ok", true);
            writer.WriteBoolean("detected", true);
            writer.WriteStartArray("preset");
            foreach (var value in values) writer.WriteBooleanValue(value);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string BuildKeyBindings(JrpKeyBindings bindings)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("ok", true);
            writer.WriteBoolean("detected", true);
            writer.WriteStartObject("keyBindings");
            writer.WriteNumber("style", bindings.Style);
            writer.WriteStartObject("layouts");
            foreach (var (style, keys) in bindings.Layouts)
            {
                writer.WriteStartArray(style.ToString(CultureInfo.InvariantCulture));
                foreach (var key in keys) writer.WriteNumberValue(key);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private string BuildInspection(ModInspection inspection)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteBoolean("ok", true);
            w.WriteBoolean("detected", true);
            w.WriteStartObject("modInspection");
            w.WriteString("id", inspection.Id);
            w.WriteString("name", inspection.DisplayName);
            w.WriteString("version", inspection.Version);
            w.WriteBoolean("alreadyInstalled", inspection.AlreadyInstalled);
            w.WriteEndObject();
            WriteLog(w);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private void WriteLog(Utf8JsonWriter w)
    {
        w.WriteStartArray("log");
        foreach (var line in log.Collect())
        {
            w.WriteStartObject();
            w.WriteString("level", line.Level);
            w.WriteString("message", line.Message);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// Version of the UMM payload bundled in the app (what install would write).
    private static string? BundledVersion(string? payloadDir)
    {
        if (string.IsNullOrWhiteSpace(payloadDir))
            return null;
        var dll = Path.Combine(payloadDir, "UnityModManager.dll");
        if (!File.Exists(dll))
            return null;
        try
        {
            using var module = ModuleDefMD.Load(File.ReadAllBytes(dll));
            return module.Assembly?.Version?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private string SafeArch(GameInstallation layout)
    {
        try { return architectureReader.Read(layout); }
        catch { return "unknown"; }
    }

    internal static string ErrorJson(string message)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteBoolean("ok", false);
            w.WriteBoolean("detected", false);
            w.WriteString("error", message);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
