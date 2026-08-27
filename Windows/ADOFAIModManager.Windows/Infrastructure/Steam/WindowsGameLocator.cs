using System.Buffers.Binary;
using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;
using NativeUmm.Domain.Installation;

namespace ADOFAIModManager.Windows.Infrastructure.Steam;

internal sealed class WindowsGameLocator : IGameLocator, IArchitectureReader
{
    private const string GameExeName = "A Dance of Fire and Ice.exe";
    private const string DataDirectoryName = "A Dance of Fire and Ice_Data";

    public GameInstallation? Locate(string? requestedPath)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(requestedPath))
            candidates.Add(NormalizeRequestedPath(requestedPath));
        var steamGame = SteamLocator.FindGame();
        if (!string.IsNullOrWhiteSpace(steamGame))
            candidates.Add(steamGame);

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            if (IsValidGameDirectory(candidate))
                return CreateInstallation(candidate);
        return null;
    }

    private static GameInstallation CreateInstallation(string gameRoot)
    {
        var root = Path.GetFullPath(gameRoot);
        var appPath = Path.Combine(root, GameExeName);
        var managedPath = Path.Combine(root, DataDirectoryName, "Managed");
        var entryAssemblyPath = Path.Combine(managedPath, UmmSpecification.EntryPoint.AssemblyName);
        return new GameInstallation(
            root,
            appPath,
            managedPath,
            entryAssemblyPath,
            Path.Combine(managedPath, "UnityModManager"),
            Path.Combine(root, "Mods"),
            entryAssemblyPath + ".original_");
    }

    public static bool IsValidGameDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;
        var managed = Path.Combine(path, DataDirectoryName, "Managed");
        return File.Exists(Path.Combine(path, GameExeName))
            && File.Exists(Path.Combine(managed, UmmSpecification.EntryPoint.AssemblyName))
            && File.Exists(Path.Combine(managed, "Assembly-CSharp.dll"));
    }

    public string Read(GameInstallation installation)
    {
        try
        {
            using var stream = File.OpenRead(installation.AppPath);
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
