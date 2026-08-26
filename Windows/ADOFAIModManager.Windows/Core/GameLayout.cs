using System.Buffers.Binary;

namespace NativeUmm;

internal sealed class GameLayout
{
    private const string GameExeName = "A Dance of Fire and Ice.exe";
    private const string DataDirectoryName = "A Dance of Fire and Ice_Data";

    private GameLayout(string gameRoot)
    {
        GameRoot = Path.GetFullPath(gameRoot);
        AppPath = Path.Combine(GameRoot, GameExeName);
        ManagedPath = Path.Combine(GameRoot, DataDirectoryName, "Managed");
        EntryAssemblyPath = Path.Combine(ManagedPath, Spec.EntryPoint.AssemblyName);
        ManagerPath = Path.Combine(ManagedPath, "UnityModManager");
        ModsPath = Path.Combine(GameRoot, "Mods");
        OriginalBackupPath = EntryAssemblyPath + ".original_";
    }

    public string GameRoot { get; }
    public string AppPath { get; }
    public string ManagedPath { get; }
    public string EntryAssemblyPath { get; }
    public string ManagerPath { get; }
    public string ModsPath { get; }
    public string OriginalBackupPath { get; }

    public static GameLayout? Detect(string? requestedPath)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(requestedPath))
            candidates.Add(NormalizeRequestedPath(requestedPath));
        var steamGame = SteamLocator.FindGame();
        if (!string.IsNullOrWhiteSpace(steamGame))
            candidates.Add(steamGame);

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            if (IsValidGameDirectory(candidate))
                return new GameLayout(candidate);
        return null;
    }

    public static bool IsValidGameDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;
        var managed = Path.Combine(path, DataDirectoryName, "Managed");
        return File.Exists(Path.Combine(path, GameExeName))
            && File.Exists(Path.Combine(managed, Spec.EntryPoint.AssemblyName))
            && File.Exists(Path.Combine(managed, "Assembly-CSharp.dll"));
    }

    public string ReadExecutableArchitectures()
    {
        try
        {
            using var stream = File.OpenRead(AppPath);
            Span<byte> dos = stackalloc byte[64];
            if (stream.Read(dos) != dos.Length || dos[0] != 'M' || dos[1] != 'Z')
                return "unknown";
            var peOffset = BinaryPrimitives.ReadInt32LittleEndian(dos[0x3c..]);
            stream.Position = peOffset;
            Span<byte> pe = stackalloc byte[6];
            if (stream.Read(pe) != pe.Length || pe[0] != 'P' || pe[1] != 'E')
                return "unknown";
            return BinaryPrimitives.ReadUInt16LittleEndian(pe[4..]) switch
            {
                0x8664 => "x64",
                0x014c => "x86",
                0xAA64 => "arm64",
                _ => "unknown"
            };
        }
        catch
        {
            return "unknown";
        }
    }

    private static string NormalizeRequestedPath(string path)
    {
        var full = Path.GetFullPath(path.Trim('"'));
        if (File.Exists(full) && Path.GetExtension(full).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(full) ?? full;
        if (Path.GetFileName(full).Equals(DataDirectoryName, StringComparison.OrdinalIgnoreCase))
            return Directory.GetParent(full)?.FullName ?? full;
        return full;
    }
}
