namespace NativeUmm;

internal static class Log
{
    [ThreadStatic] private static List<LogLine>? _buffer;

    public static void Begin() => _buffer = [];
    public static void Info(string message) => _buffer?.Add(new LogLine("info", message));
    public static void Warn(string message) => _buffer?.Add(new LogLine("warn", message));
    public static void Fail(string message) => _buffer?.Add(new LogLine("error", message));
    public static IReadOnlyList<LogLine> Collect() => _buffer ?? [];
}

internal readonly record struct LogLine(string Level, string Message);

internal static class AppData
{
    public static string Root => Ensure(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ADOFAIModManager",
        "Data"));

    public static string Cache => Ensure(Path.Combine(Root, "cache"));
    public static string RemovedMods => Ensure(Path.Combine(Root, "removed-mods"));
    public static string RemovedUmm => Ensure(Path.Combine(Root, "removed-umm"));

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}

internal static class Spec
{
    public const string OfficialPayloadUrl = "https://adof.ai/umm";

    public static readonly EntryPointInfo EntryPoint = new(
        "UnityEngine.CoreModule.dll",
        "UnityEngine.MonoBehaviour",
        ".cctor",
        InsertPlace.Before);

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
