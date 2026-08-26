namespace NativeUmm;

/// In-memory log sink. Replaces the TUI's colored Console output; the Swift UI
/// renders whatever we collect here per call.
internal static class Log
{
    [ThreadStatic] private static List<LogLine>? _buffer;

    public static void Begin() => _buffer = new List<LogLine>();
    public static void Info(string message) => _buffer?.Add(new LogLine("info", message));
    public static void Warn(string message) => _buffer?.Add(new LogLine("warn", message));
    public static void Fail(string message) => _buffer?.Add(new LogLine("error", message));
    public static IReadOnlyList<LogLine> Collect() => _buffer ?? (IReadOnlyList<LogLine>)Array.Empty<LogLine>();
}

internal readonly record struct LogLine(string Level, string Message);

/// App data lives under ~/Library/Application Support/ADOFAI Mod Manager:
/// removed-mod backups, download caches, etc. (Kept out of the game's Mods
/// folder so removed mods don't keep loading.)
internal static class AppData
{
    public static string Root => Ensure(Path.Combine(
        Environment.GetEnvironmentVariable("ADOFAI_MOD_MANAGER_DATA") ??
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetEnvironmentVariable("ADOFAI_MOD_MANAGER_DATA") is null
            ? Path.Combine("Library", "Application Support", "ADOFAI Mod Manager")
            : ""));

    public static string Cache => Ensure(Path.Combine(Root, "cache"));
    public static string RemovedMods => Ensure(Path.Combine(Root, "removed-mods"));

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}

/// Constants describing the ADOFAI hook target and UMM payload shared by the
/// native macOS and Windows front ends.
internal static class Spec
{
    public const string OfficialPayloadUrl = "https://adof.ai/umm";

    public static readonly EntryPointInfo EntryPoint = new(
        "UnityEngine.CoreModule.dll",
        "UnityEngine.MonoBehaviour",
        ".cctor",
        InsertPlace.Before);

    public static readonly EntryPointInfo StartingPoint = new(
        "Assembly-CSharp.dll",
        "ADOStartup",
        "Startup",
        InsertPlace.Before);

    public static readonly EntryPointInfo UIStartingPoint = new(
        "Assembly-CSharp.dll",
        "ADOStartup",
        "Startup",
        InsertPlace.After);

    public static readonly string[] PayloadFiles =
    [
        "UnityModManager.dll",
        "UnityModManager.xml",
        "0Harmony.dll",
        "dnlib.dll",
        "System.Xml.dll"
    ];
}

internal sealed record EntryPointInfo(string AssemblyName, string TypeName, string MethodName, InsertPlace Place)
{
    public string ToConfigString()
    {
        var method = MethodName switch
        {
            ".cctor" => "cctor",
            ".ctor" => "ctor",
            _ => MethodName
        };
        return $"[{AssemblyName}]{TypeName}.{method}:{Place}";
    }
}

internal enum InsertPlace
{
    Before,
    After
}

internal sealed class InstallStatus
{
    public bool HookInstalled { get; set; }
    public bool ManagerInstalled { get; set; }
    public string? ManagerVersion { get; set; }
    public bool HasOriginalBackup { get; set; }
    public string OriginalBackupPath { get; set; } = "";
    public string? Warning { get; set; }
}

internal sealed record Payload(Dictionary<string, string> Files, bool IsBundled = false);
