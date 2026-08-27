namespace NativeUmm.Domain.Installation;

public sealed class InstallStatus
{
    public bool HookInstalled { get; set; }
    public bool ManagerInstalled { get; set; }
    public string? ManagerVersion { get; set; }
    public bool HasOriginalBackup { get; set; }
    public string OriginalBackupPath { get; set; } = "";
    public string? Warning { get; set; }
}

public sealed record Payload(IReadOnlyDictionary<string, string> Files, bool IsBundled = false);

public enum InsertPlace
{
    Before,
    After
}

public sealed record EntryPointInfo(string AssemblyName, string TypeName, string MethodName, InsertPlace Place)
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

public static class UmmSpecification
{
    public const string OfficialPayloadUrl = "https://adof.ai/umm";

    public static readonly EntryPointInfo EntryPoint = new(
        "UnityEngine.CoreModule.dll", "UnityEngine.MonoBehaviour", ".cctor", InsertPlace.Before);

    public static readonly EntryPointInfo StartingPoint = new(
        "Assembly-CSharp.dll", "ADOStartup", "Startup", InsertPlace.Before);

    public static readonly EntryPointInfo UIStartingPoint = new(
        "Assembly-CSharp.dll", "ADOStartup", "Startup", InsertPlace.After);

    public static readonly IReadOnlyList<string> PayloadFiles =
    [
        "UnityModManager.dll",
        "UnityModManager.xml",
        "0Harmony.dll",
        "dnlib.dll",
        "System.Xml.dll"
    ];
}
