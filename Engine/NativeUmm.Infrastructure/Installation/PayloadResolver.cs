using System.IO.Compression;
using System.Security.Cryptography;
using dnlib.DotNet;
using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;
using NativeUmm.Domain.Installation;

namespace NativeUmm.Infrastructure.Installation;

internal static class PayloadResolver
{
    public static async Task<Payload> ResolveAsync(
        GameInstallation installation,
        string? bundledPayloadDirectory,
        bool forceDownload,
        IOperationLog log,
        IAppDataPaths appData)
    {
        var bundled = TryBuild(BundledDirectories(bundledPayloadDirectory).Distinct(), isBundled: true);
        if (bundled is not null) return bundled;

        var candidates = CandidateDirectories(installation, appData).Distinct().ToList();
        if (!forceDownload && TryBuild(candidates) is { } local) return local;

        candidates.Insert(0, await DownloadOfficialPayloadAsync(log, appData));
        return TryBuild(candidates)
            ?? throw new InvalidOperationException("Could not resolve UnityModManager payload files.");
    }

    private static IEnumerable<string> BundledDirectories(string? directory)
    {
        if (!string.IsNullOrWhiteSpace(directory)) yield return directory;
        yield return Path.Combine(AppContext.BaseDirectory, "payload");
        yield return Path.Combine(AppContext.BaseDirectory, "UnityModManagerInstaller");
    }

    private static IEnumerable<string> CandidateDirectories(GameInstallation installation, IAppDataPaths appData)
    {
        yield return Path.Combine(AppContext.BaseDirectory, "payload");
        yield return Path.Combine(AppContext.BaseDirectory, "UnityModManagerInstaller");
        yield return installation.ManagerPath;
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            yield return Path.Combine(directory.FullName, "lib");
            yield return Path.Combine(directory.FullName, "UnityModManagerInstaller");
        }
        yield return CachePath(appData);
    }

    private static Payload? TryBuild(IEnumerable<string> directories, bool isBundled = false)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in directories.Where(Directory.Exists))
        foreach (var name in UmmSpecification.PayloadFiles)
        {
            if (files.ContainsKey(name)) continue;
            var direct = Path.Combine(directory, name);
            if (File.Exists(direct) && IsCompatibleFile(name, direct)) files[name] = direct;
            else if (Directory.EnumerateFiles(directory, name, SearchOption.AllDirectories)
                     .FirstOrDefault(path => IsCompatibleFile(name, path)) is { } nested)
                files[name] = nested;
        }

        return files.ContainsKey("UnityModManager.dll")
               && files.ContainsKey("0Harmony.dll")
               && files.ContainsKey("dnlib.dll")
            ? new Payload(files, isBundled) : null;
    }

    private static bool IsCompatibleFile(string name, string path)
    {
        if (!name.Equals("0Harmony.dll", StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            using var module = ModuleDefMD.Load(File.ReadAllBytes(path));
            return !module.GetAssemblyRefs().Any(reference =>
                reference.Name.String.Equals("System.Runtime", StringComparison.OrdinalIgnoreCase)
                && reference.Version is { Major: >= 5 });
        }
        catch { return false; }
    }

    private static string CachePath(IAppDataPaths appData) => Path.Combine(appData.Cache, "payload");

    private static async Task<string> DownloadOfficialPayloadAsync(IOperationLog log, IAppDataPaths appData)
    {
        var cache = CachePath(appData);
        Directory.CreateDirectory(cache);
        var zipPath = Path.Combine(cache, "UnityModManager.zip");
        log.Info("Downloading UnityModManager payload...");
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("adofai-mod-manager/1.0");
        var bytes = await client.GetByteArrayAsync(UmmSpecification.OfficialPayloadUrl);
        await File.WriteAllBytesAsync(zipPath, bytes);

        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var name in UmmSpecification.PayloadFiles)
        {
            var entry = zip.Entries
                .Where(candidate => Path.GetFileName(candidate.FullName).Equals(name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => EntryPriority(candidate.FullName, name))
                .FirstOrDefault(candidate => IsCompatibleArchiveFile(name, candidate));
            entry?.ExtractToFile(Path.Combine(cache, name), overwrite: true);
        }

        var stamp = Convert.ToHexString(SHA256.HashData(bytes))[..16];
        await File.WriteAllTextAsync(Path.Combine(cache, "payload.sha256.txt"), stamp);
        return cache;
    }

    private static int EntryPriority(string fullName, string fileName)
    {
        var normalized = fullName.Replace('\\', '/').TrimStart('/');
        return normalized.Equals(fileName, StringComparison.OrdinalIgnoreCase)
               || normalized.Equals($"UnityModManagerInstaller/{fileName}", StringComparison.OrdinalIgnoreCase)
            ? 0 : 10 + normalized.Count(character => character == '/');
    }

    private static bool IsCompatibleArchiveFile(string name, ZipArchiveEntry entry)
    {
        if (!name.Equals("0Harmony.dll", StringComparison.OrdinalIgnoreCase)) return true;
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"adofai-harmony-{Guid.NewGuid():N}.dll");
        try
        {
            entry.ExtractToFile(temporaryPath);
            return IsCompatibleFile(name, temporaryPath);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
