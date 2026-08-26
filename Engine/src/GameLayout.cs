using System.Buffers.Binary;

namespace NativeUmm;

internal sealed class GameLayout
{
    private GameLayout(string gameRoot, string appPath)
    {
        GameRoot = gameRoot;
        AppPath = appPath;
        ManagedPath = Path.Combine(appPath, "Contents", "Resources", "Data", "Managed");
        EntryAssemblyPath = Path.Combine(ManagedPath, Spec.EntryPoint.AssemblyName);
        ManagerPath = Path.Combine(ManagedPath, "UnityModManager");
        ModsPath = Path.Combine(gameRoot, "Mods");
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
        foreach (var candidate in CandidatePaths(requestedPath))
        {
            var expanded = ExpandHome(candidate);
            var app = ResolveAppPath(expanded);
            if (app is null)
                continue;

            var root = Directory.GetParent(app)?.FullName;
            if (root is null)
                continue;

            var layout = new GameLayout(root, app);
            if (File.Exists(layout.EntryAssemblyPath) && File.Exists(Path.Combine(layout.ManagedPath, "Assembly-CSharp.dll")))
                return layout;
        }

        return null;
    }

    public string ReadExecutableArchitectures()
    {
        var executable = Path.Combine(AppPath, "Contents", "MacOS", "ADanceOfFireAndIce");
        if (!File.Exists(executable))
            return "missing";

        try
        {
            return ReadMachOArchitectures(executable);
        }
        catch
        {
            return "unknown";
        }
    }

    // Mach-O magics, read big-endian so each on-disk byte order maps to one value.
    private const uint FatMagic = 0xCAFEBABE, FatMagic64 = 0xCAFEBABF;
    private const uint Thin64LE = 0xCFFAEDFE, Thin32LE = 0xCEFAEDFE; // cputype little-endian
    private const uint Thin64BE = 0xFEEDFACF, Thin32BE = 0xFEEDFACE; // cputype big-endian

    private const int CpuArchAbi64 = 0x01000000;
    private const int CpuTypeX86 = 7, CpuTypeArm = 12;
    private const int CpuTypeX86_64 = CpuTypeX86 | CpuArchAbi64;
    private const int CpuTypeArm64 = CpuTypeArm | CpuArchAbi64;
    private const int CpuSubtypeArm64E = 2;

    /// Parses the Mach-O header directly to list architectures (e.g. "x86_64 arm64"),
    /// matching `lipo -archs`. Reading the bytes ourselves avoids spawning
    /// /usr/bin/lipo, whose shim pops the "install command line developer tools"
    /// dialog on Macs without Xcode Command Line Tools installed.
    private static string ReadMachOArchitectures(string path)
    {
        using var fs = File.OpenRead(path);
        Span<byte> head = stackalloc byte[8];
        if (!TryReadExact(fs, head))
            return "unknown";

        uint magic = BinaryPrimitives.ReadUInt32BigEndian(head);

        // Fat/universal binary: slice count and each slice's cputype are big-endian.
        if (magic is FatMagic or FatMagic64)
        {
            uint count = BinaryPrimitives.ReadUInt32BigEndian(head[4..]);
            if (count == 0 || count > 32)
                return "unknown";
            int stride = magic == FatMagic64 ? 32 : 20; // sizeof fat_arch_64 / fat_arch
            var archs = new List<string>((int)count);
            Span<byte> slice = stackalloc byte[8]; // cputype + cpusubtype
            for (uint i = 0; i < count; i++)
            {
                fs.Position = 8 + (long)i * stride;
                if (!TryReadExact(fs, slice))
                    break;
                archs.Add(ArchName(BinaryPrimitives.ReadInt32BigEndian(slice),
                                   BinaryPrimitives.ReadInt32BigEndian(slice[4..])));
            }
            return archs.Count > 0 ? string.Join(' ', archs) : "unknown";
        }

        // Thin Mach-O: cputype (offset 4) + cpusubtype (offset 8) follow the magic.
        bool littleEndian = magic is Thin64LE or Thin32LE;
        if (!littleEndian && magic is not (Thin64BE or Thin32BE))
            return "unknown";

        int cpuType = littleEndian
            ? BinaryPrimitives.ReadInt32LittleEndian(head[4..])
            : BinaryPrimitives.ReadInt32BigEndian(head[4..]);

        Span<byte> sub = stackalloc byte[4];
        fs.Position = 8;
        int cpuSub = TryReadExact(fs, sub)
            ? (littleEndian ? BinaryPrimitives.ReadInt32LittleEndian(sub)
                            : BinaryPrimitives.ReadInt32BigEndian(sub))
            : 0;
        return ArchName(cpuType, cpuSub);
    }

    private static string ArchName(int cpuType, int cpuSubtype) => cpuType switch
    {
        CpuTypeX86_64 => "x86_64",
        CpuTypeArm64  => (cpuSubtype & 0x00FFFFFF) == CpuSubtypeArm64E ? "arm64e" : "arm64",
        CpuTypeX86    => "i386",
        CpuTypeArm    => "arm",
        _             => $"unknown(0x{cpuType:x})"
    };

    private static bool TryReadExact(Stream stream, Span<byte> buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = stream.Read(buffer[read..]);
            if (n == 0)
                return false;
            read += n;
        }
        return true;
    }

    private static IEnumerable<string> CandidatePaths(string? requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
            yield return requestedPath;

        yield return "~/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice";
        yield return "~/Library/Application Support/com.valvesoftware.Steam/steamapps/common/A Dance of Fire and Ice";
        yield return "~/.steam/steam/steamapps/common/A Dance of Fire and Ice";

        var libraryFolders = ExpandHome("~/Library/Application Support/Steam/steamapps/libraryfolders.vdf");
        if (File.Exists(libraryFolders))
        {
            foreach (var line in File.ReadLines(libraryFolders))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("\"path\"", StringComparison.Ordinal))
                    continue;

                var parts = trimmed.Split('"', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    yield return Path.Combine(parts[^1].Replace(@"\\", "/"), "steamapps", "common", "A Dance of Fire and Ice");
            }
        }
    }

    private static string? ResolveAppPath(string path)
    {
        if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(path))
            return path;

        if (Directory.Exists(Path.Combine(path, "Contents", "Resources", "Data", "Managed")))
            return path;

        var direct = Path.Combine(path, "ADanceOfFireAndIce.app");
        if (Directory.Exists(direct))
            return direct;

        if (!Directory.Exists(path))
            return null;

        return Directory.EnumerateDirectories(path, "*.app", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(p => Path.GetFileName(p).Contains("Dance", StringComparison.OrdinalIgnoreCase) ||
                                 Path.GetFileName(p).Contains("ADOFAI", StringComparison.OrdinalIgnoreCase));
    }

    private static string ExpandHome(string path)
    {
        if (path == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (path.StartsWith("~/", StringComparison.Ordinal))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);

        return path;
    }
}

