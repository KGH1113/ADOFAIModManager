using System.Buffers.Binary;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;
using NativeUmm.Domain.Installation;

namespace NativeUmm.Cli.Platform.Windows;

[SupportedOSPlatform("windows")]
internal sealed partial class WindowsGameLocator : IGameLocator, IArchitectureReader
{
    private const string Exe = "A Dance of Fire and Ice.exe";
    private const string Data = "A Dance of Fire and Ice_Data";

    public GameInstallation? Locate(string? requestedPath)
    {
        var candidates = string.IsNullOrWhiteSpace(requestedPath)
            ? Libraries().Select(p => Path.Combine(p, "steamapps", "common", "A Dance of Fire and Ice"))
            : [Normalize(requestedPath)];
        foreach (var candidate in candidates)
        {
            var root = Path.GetFullPath(candidate);
            var managed = Path.Combine(root, Data, "Managed");
            var entry = Path.Combine(managed, UmmSpecification.EntryPoint.AssemblyName);
            if (File.Exists(Path.Combine(root, Exe)) && File.Exists(entry) && File.Exists(Path.Combine(managed, "Assembly-CSharp.dll")))
                return new GameInstallation(root, Path.Combine(root, Exe), managed, entry,
                    Path.Combine(managed, "UnityModManager"), Path.Combine(root, "Mods"), entry + ".original_");
        }
        return null;
    }

    public string Read(GameInstallation game)
    {
        using var file = File.OpenRead(game.AppPath);
        Span<byte> dos = stackalloc byte[64];
        if (file.Read(dos) != dos.Length || dos[0] != 'M' || dos[1] != 'Z') return "unknown";
        file.Position = BinaryPrimitives.ReadInt32LittleEndian(dos[0x3c..]);
        Span<byte> pe = stackalloc byte[6];
        if (file.Read(pe) != pe.Length || pe[0] != 'P' || pe[1] != 'E') return "unknown";
        return BinaryPrimitives.ReadUInt16LittleEndian(pe[4..]) switch
        { 0x8664 => "x64", 0x014c => "x86", 0xAA64 => "arm64", _ => "unknown" };
    }

    private static string Normalize(string path)
    {
        var full = Path.GetFullPath(path.Trim('"'));
        if (File.Exists(full)) return Path.GetDirectoryName(full)!;
        return Path.GetFileName(full).Equals(Data, StringComparison.OrdinalIgnoreCase)
            ? Directory.GetParent(full)!.FullName : full;
    }

    private static IEnumerable<string> Libraries()
    {
        var roots = new List<string>();
        AddRegistry(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath", roots);
        AddRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", roots);
        AddRegistry(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath", roots);
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        var libraries = new HashSet<string>(roots.Where(Directory.Exists), StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            try
            {
                if (File.Exists(vdf)) foreach (Match match in LibraryPath().Matches(File.ReadAllText(vdf)))
                    libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return libraries;
    }

    private static void AddRegistry(RegistryKey hive, string path, string name, List<string> roots)
    {
        try { using var key = hive.OpenSubKey(path); if (key?.GetValue(name) is string value) roots.Add(value); }
        catch (Exception) { /* Optional discovery; manual folder selection remains available. */ }
    }

    [GeneratedRegex("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPath();
}
