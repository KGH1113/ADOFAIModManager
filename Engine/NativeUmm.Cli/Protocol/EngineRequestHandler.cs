using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using dnlib.DotNet;
using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;
using NativeUmm.Domain.Installation;
using NativeUmm.Domain.Mods;

namespace NativeUmm.Cli.Protocol;

internal sealed class EngineRequestHandler(
    IGameLocator gameLocator,
    IArchitectureReader architectureReader,
    IUmmInstaller installer,
    IModService modService,
    IOperationLog log)
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
                error: "A Dance of Fire and Ice was not found. Select the game .app.");

        var bundledVersion = BundledVersion(payloadDir);

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
                return BuildLogTail(layout);
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

    private static string BuildLogTail(GameInstallation layout)
    {
        var logPath = Path.Combine(layout.ManagerPath, "Log.txt");
        var exists = File.Exists(logPath);
        var text = exists
            ? string.Join("\n", File.ReadLines(logPath).TakeLast(400))
            : "";

        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteBoolean("ok", true);
            w.WriteBoolean("detected", true);
            w.WriteBoolean("logExists", exists);
            w.WriteString("logText", text);
            w.WriteEndObject();
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
