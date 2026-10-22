using System.Buffers.Binary;
using System.Text.RegularExpressions;
using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;
using NativeUmm.Domain.Installation;

namespace NativeUmm.Cli.Platform.Linux;

public sealed partial class LinuxGameLocator : IGameLocator, IArchitectureReader
{
    public GameInstallation? Locate(string? requestedPath)
    {
        var candidates = string.IsNullOrWhiteSpace(requestedPath)
            ? Libraries().Select(p => Path.Combine(p, "steamapps", "common", "A Dance of Fire and Ice"))
            : [Normalize(requestedPath)];
        foreach (var candidate in candidates)
        {
            foreach (var executable in new[] { "ADanceOfFireAndIce.x86_64", "ADanceOfFireAndIce", "A Dance of Fire and Ice.exe" })
            {
                var root = Path.GetFullPath(candidate);
                var data = executable.EndsWith(".exe", StringComparison.Ordinal)
                    ? "A Dance of Fire and Ice_Data" : "ADanceOfFireAndIce_Data";
                var managed = Path.Combine(root, data, "Managed");
                var entry = Path.Combine(managed, UmmSpecification.EntryPoint.AssemblyName);
                if (File.Exists(Path.Combine(root, executable)) && File.Exists(entry) && File.Exists(Path.Combine(managed, "Assembly-CSharp.dll")))
                    return new GameInstallation(root, Path.Combine(root, executable), managed, entry,
                        Path.Combine(managed, "UnityModManager"), Path.Combine(root, "Mods"), entry + ".original_");
            }
        }
        return null;
    }

    public string Read(GameInstallation game)
    {
        using var file = File.OpenRead(game.AppPath);
        Span<byte> header = stackalloc byte[64];
        if (file.Read(header) != header.Length) return "unknown";
        if (header[0] == 0x7f && header[1] == 'E' && header[2] == 'L' && header[3] == 'F')
        {
            var machine = header[5] switch {
                1 => BinaryPrimitives.ReadUInt16LittleEndian(header[18..]),
                2 => BinaryPrimitives.ReadUInt16BigEndian(header[18..]),
                _ => 0
            };
            return machine switch { 62 => "x64", 183 => "arm64", 3 => "x86", _ => "unknown" };
        }
        if (header[0] != 'M' || header[1] != 'Z') return "unknown";
        var offset = BinaryPrimitives.ReadInt32LittleEndian(header[0x3c..]);
        if (offset < 0 || offset > file.Length - 6) return "unknown";
        file.Position = offset;
        Span<byte> pe = stackalloc byte[6];
        if (file.Read(pe) != pe.Length || pe[0] != 'P' || pe[1] != 'E' || pe[2] != 0 || pe[3] != 0) return "unknown";
        return BinaryPrimitives.ReadUInt16LittleEndian(pe[4..]) switch
        { 0x8664 => "x64", 0x014c => "x86", 0xAA64 => "arm64", _ => "unknown" };
    }

    private static string Normalize(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var full = Path.GetFullPath(path.StartsWith("~/", StringComparison.Ordinal) ? Path.Combine(home, path[2..]) : path);
        if (File.Exists(full)) return Path.GetDirectoryName(full)!;
        if (Path.GetFileName(full) == "Managed") full = Directory.GetParent(full)!.FullName;
        return Path.GetFileName(full).EndsWith("_Data", StringComparison.Ordinal) ? Directory.GetParent(full)!.FullName : full;
    }

    private static IEnumerable<string> Libraries()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new[] { ".steam/steam", ".steam/root", ".local/share/Steam", ".var/app/com.valvesoftware.Steam/.local/share/Steam" }
            .Select(p => Path.Combine(home, p)).ToArray();
        var libraries = new HashSet<string>(roots.Where(Directory.Exists), StringComparer.Ordinal);
        foreach (var root in roots)
        {
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            try {
                if (File.Exists(vdf)) foreach (Match match in LibraryPath().Matches(File.ReadAllText(vdf)))
                    libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return libraries;
    }

    [GeneratedRegex("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPath();
}
