using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NativeUmm;

/// A single entry of a mod's Info.json "Requirements", resolved against what is
/// actually present in Mods/. State mirrors the in-game manager's tags:
/// "OK" | "Missing" | "Inactive" | "Outdated".
internal sealed record ModRequirement(string Id, string? Version, string State);

internal sealed record ModInfo(
    string Id,
    string DisplayName,
    string Version,
    string? ManagerVersion,
    string? HomePage,
    string Status,
    string Path,
    bool Installed,
    bool Enabled,
    IReadOnlyList<ModRequirement> Requirements);

internal sealed record ModInspection(
    string Id,
    string DisplayName,
    string Version,
    bool AlreadyInstalled);

/// Lists and installs UMM mods under the game's Mods/ folder — mirrors the
/// "Mods" tab of the original Windows installer.
internal static class ModManager
{
    public static List<ModInfo> List(GameLayout layout)
    {
        var result = new List<ModInfo>();
        AddFrom(result, layout.ModsPath, installed: true);
        result.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        var disabledIds = ReadDisabledIds(layout);
        for (var i = 0; i < result.Count; i++)
            result[i] = result[i] with { Enabled = !disabledIds.Contains(result[i].Id) };
        ResolveRequirements(result, disabledIds);
        return result;
    }

    // MARK: requirements

    /// Matches UMM's own requirement syntax: "ModId-1.2.3" carries a minimum
    /// version, a bare "ModId" does not.
    private static readonly Regex RequirementRegex = new(@"(.*)-(\d+\.\d+\.\d+).*", RegexOptions.Compiled);

    /// Fills in each requirement's State now that the whole mod set is known.
    /// Mirrors the in-game manager's precedence: missing beats inactive beats
    /// outdated. Removed-mod replacement backups are intentionally not listed.
    private static void ResolveRequirements(List<ModInfo> mods, HashSet<string> disabledIds)
    {
        var byId = new Dictionary<string, ModInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods)
            if (!byId.ContainsKey(mod.Id))
                byId[mod.Id] = mod;

        for (var i = 0; i < mods.Count; i++)
        {
            if (mods[i].Requirements.Count == 0)
                continue;

            var resolved = new List<ModRequirement>(mods[i].Requirements.Count);
            foreach (var req in mods[i].Requirements)
            {
                string state;
                if (!byId.TryGetValue(req.Id, out var found))
                    state = "Missing";
                else if (disabledIds.Contains(found.Id))
                    state = "Inactive";
                else if (req.Version is not null && CompareVersions(req.Version, found.Version) > 0)
                    state = "Outdated";
                else
                    state = "OK";

                resolved.Add(req with { State = state });
            }

            mods[i] = mods[i] with { Requirements = resolved };
        }
    }

    /// Reads the mod on/off toggles the in-game manager persists next to its DLL.
    /// Absent file (manager never launched) means nothing is disabled.
    private static HashSet<string> ReadDisabledIds(GameLayout layout)
    {
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paramsPath = System.IO.Path.Combine(layout.ManagerPath, "Params.xml");
        if (!File.Exists(paramsPath))
            return disabled;

        try
        {
            foreach (var element in XDocument.Load(paramsPath).Descendants("Mod"))
            {
                var id = (string?)element.Attribute("Id");
                if (string.IsNullOrEmpty(id))
                    continue;
                if (!bool.TryParse((string?)element.Attribute("Enabled") ?? "true", out var enabled) || !enabled)
                    disabled.Add(id);
            }
        }
        catch
        {
            // Unreadable Params.xml just means we can't tell — treat all as enabled.
        }

        return disabled;
    }

    private static List<ModRequirement> ParseRequirements(JsonElement element)
    {
        var result = new List<ModRequirement>();
        if (!element.TryGetProperty("Requirements", out var array) || array.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                continue;
            var raw = item.GetString();
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var match = RequirementRegex.Match(raw);
            var id = match.Success ? match.Groups[1].Value : raw;
            var version = match.Success ? match.Groups[2].Value : null;
            if (result.Any(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                continue;

            result.Add(new ModRequirement(id, version, "OK"));
        }

        return result;
    }

    /// Lenient dotted-numeric compare (non-digits stripped per component), so the
    /// tags agree with the in-game manager on versions like "1.2.0b".
    private static int CompareVersions(string left, string right) =>
        ToVersion(left).CompareTo(ToVersion(right));

    private static Version ToVersion(string raw)
    {
        var parts = raw.Split('.');
        var numbers = new int[4];
        for (var i = 0; i < 4; i++)
        {
            if (i >= parts.Length)
                break;
            var digits = Regex.Replace(parts[i], @"\D", "");
            numbers[i] = int.TryParse(digits, out var value) ? value : 0;
        }
        return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
    }

    private static void AddFrom(List<ModInfo> result, string root, bool installed)
    {
        if (!Directory.Exists(root))
            return;

        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            // Ignore leftover assembly/in-place backups from older builds.
            if (Path.GetFileName(dir).Contains("nativeumm_backup", StringComparison.OrdinalIgnoreCase))
                continue;

            var infoPath = FindInfoJson(dir);
            if (infoPath is null)
                continue;

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(infoPath));
                var element = doc.RootElement;

                var id = GetString(element, "Id");
                if (string.IsNullOrEmpty(id))
                    id = Path.GetFileName(dir);
                var name = GetString(element, "DisplayName");
                if (string.IsNullOrEmpty(name))
                    name = id;
                var version = GetString(element, "Version");
                var manager = GetString(element, "ManagerVersion");
                var home = GetString(element, "HomePage");

                result.Add(new ModInfo(id, name, version,
                    string.IsNullOrEmpty(manager) ? null : manager,
                    string.IsNullOrEmpty(home) ? null : home,
                    installed ? "OK" : "Uninstalled", dir, installed, installed,
                    ParseRequirements(element)));
            }
            catch
            {
                var name = Path.GetFileName(dir);
                result.Add(new ModInfo(name, name, "", null, null,
                    installed ? "Invalid Info.json" : "Uninstalled", dir, installed, installed,
                    Array.Empty<ModRequirement>()));
            }
        }
    }

    /// Removes an installed mod by Id, moving its folder out of Mods/ into the
    /// app's removed-mods backup so the game no longer loads it.
    /// Uninstall: moves an installed mod out of Mods/ into removed-mods (reversible).
    public static void Uninstall(GameLayout layout, string modPath)
    {
        if (!IsInside(modPath, layout.ModsPath) || !Directory.Exists(modPath))
        {
            Log.Warn("Mod not found in Mods folder.");
            return;
        }

        var dest = Path.Combine(AppData.RemovedMods, $"{Path.GetFileName(modPath)}_{DateTime.Now:yyyyMMdd_HHmmss}");
        MoveDirectory(modPath, dest);
        Log.Info($"Uninstalled '{Path.GetFileName(modPath)}' (kept in removed-mods).");
    }

    /// Restore: moves a previously uninstalled mod back from removed-mods into Mods/.
    public static void Restore(GameLayout layout, string modPath)
    {
        if (!IsInside(modPath, AppData.RemovedMods) || !Directory.Exists(modPath))
        {
            Log.Warn("Removed mod not found.");
            return;
        }

        var id = ReadId(modPath) ?? Path.GetFileName(modPath);
        Directory.CreateDirectory(layout.ModsPath);
        var target = Path.Combine(layout.ModsPath, id);
        if (Directory.Exists(target))
        {
            Log.Warn($"'{id}' is already installed.");
            return;
        }

        MoveDirectory(modPath, target);
        Log.Info($"Reinstalled '{id}'.");
    }

    /// Remove: permanently deletes a mod's folder (installed or removed). Guarded
    /// so it can only delete inside Mods/ or removed-mods.
    public static void Remove(GameLayout layout, string modPath)
    {
        if ((!IsInside(modPath, layout.ModsPath) && !IsInside(modPath, AppData.RemovedMods)) || !Directory.Exists(modPath))
        {
            Log.Warn("Mod not found.");
            return;
        }

        Directory.Delete(modPath, recursive: true);
        Log.Info($"Permanently removed '{Path.GetFileName(modPath)}'.");
    }

    private static bool IsInside(string path, string root)
    {
        var full = Path.GetFullPath(path);
        var r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return full.StartsWith(r + Path.DirectorySeparatorChar, comparison);
    }

    private static string? ReadId(string dir)
    {
        var info = FindInfoJson(dir);
        if (info is null)
            return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(info));
            var id = GetString(doc.RootElement, "Id");
            return string.IsNullOrEmpty(id) ? null : id;
        }
        catch
        {
            return null;
        }
    }

    /// Downloads a mod zip from a URL into the cache, then installs it.
    public static async Task InstallFromUrlAsync(GameLayout layout, string url)
    {
        var cacheDir = Path.Combine(AppData.Cache, "recommended");
        Directory.CreateDirectory(cacheDir);

        var name = Path.GetFileName(new Uri(url).AbsolutePath);
        if (string.IsNullOrEmpty(name)) name = "mod";
        if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) name += ".zip";
        var zipPath = Path.Combine(cacheDir, name);

        Log.Info($"Downloading {name}...");
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("adofai-mod-manager/1.0");
            var bytes = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(zipPath, bytes);
        }

        Install(layout, zipPath);
    }

    public static void Install(GameLayout layout, string zipPath)
    {
        Directory.CreateDirectory(layout.ModsPath);
        using var zip = OpenAndValidate(zipPath);
        var descriptor = ReadDescriptor(zip);
        var staging = Path.Combine(layout.ModsPath, $".adofai-import-{Guid.NewGuid():N}");
        var target = Path.Combine(layout.ModsPath, descriptor.Id);
        var backup = "";

        try
        {
            Directory.CreateDirectory(staging);
            var count = ExtractMod(zip, descriptor.SourceDirectory, staging);
            if (Directory.Exists(target))
            {
                backup = Path.Combine(AppData.RemovedMods,
                    $"{descriptor.Id}_{DateTime.Now:yyyyMMdd_HHmmss}_replaced");
                MoveDirectory(target, backup);
            }
            Directory.Move(staging, target);
            Log.Info($"Installed {count} file(s) from {Path.GetFileName(zipPath)}");
        }
        catch
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            if (backup.Length > 0 && Directory.Exists(backup) && !Directory.Exists(target))
                MoveDirectory(backup, target);
            throw;
        }
    }

    public static ModInspection Inspect(GameLayout layout, string zipPath)
    {
        using var zip = OpenAndValidate(zipPath);
        var descriptor = ReadDescriptor(zip);
        return new ModInspection(descriptor.Id, descriptor.DisplayName, descriptor.Version,
            Directory.Exists(Path.Combine(layout.ModsPath, descriptor.Id)));
    }

    public static void SetEnabled(GameLayout layout, string modId, bool enabled)
    {
        ValidateModId(modId);
        var installed = List(layout).FirstOrDefault(mod =>
            mod.Installed && mod.Id.Equals(modId, StringComparison.OrdinalIgnoreCase));
        if (installed is null)
            throw new InvalidOperationException("The selected mod is not installed.");

        Directory.CreateDirectory(layout.ManagerPath);
        var paramsPath = Path.Combine(layout.ManagerPath, "Params.xml");
        XDocument document;
        try
        {
            document = File.Exists(paramsPath)
                ? XDocument.Load(paramsPath, LoadOptions.PreserveWhitespace)
                : new XDocument(new XElement("Param"));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("UMM settings could not be read.", ex);
        }

        var root = document.Root ?? throw new InvalidOperationException("UMM settings are invalid.");
        var list = root.Element("ModParams");
        if (list is null)
        {
            list = new XElement("ModParams");
            root.Add(list);
        }
        var entry = list.Elements("Mod").FirstOrDefault(element =>
            string.Equals((string?)element.Attribute("Id"), modId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            entry = new XElement("Mod", new XAttribute("Id", modId));
            list.Add(entry);
        }
        entry.SetAttributeValue("Enabled", enabled);

        var temporaryPath = paramsPath + $".tmp-{Guid.NewGuid():N}";
        var backupPath = paramsPath + ".adofai-mod-manager.bak";
        try
        {
            document.Save(temporaryPath);
            if (File.Exists(paramsPath))
                File.Copy(paramsPath, backupPath, overwrite: true);
            File.Move(temporaryPath, paramsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        Log.Info($"Set '{modId}' to {(enabled ? "enabled" : "disabled")}.");
    }

    private const int MaximumEntries = 10_000;
    private const long MaximumExpandedBytes = 2L * 1024 * 1024 * 1024;
    private const long CompressionRatioCheckThreshold = 8L * 1024 * 1024;
    private const long MaximumCompressionRatio = 250;

    private sealed record ArchiveDescriptor(
        string Id, string DisplayName, string Version, string SourceDirectory);

    private static ZipArchive OpenAndValidate(string zipPath)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("Zip not found: " + zipPath);
        if (!Path.GetExtension(zipPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected file is not a ZIP archive.");

        var zip = ZipFile.OpenRead(zipPath);
        try
        {
            if (zip.Entries.Count > MaximumEntries)
                throw new InvalidOperationException("The archive contains too many files.");
            long expandedBytes = 0;
            foreach (var entry in zip.Entries)
            {
                expandedBytes = checked(expandedBytes + entry.Length);
                if (expandedBytes > MaximumExpandedBytes)
                    throw new InvalidOperationException("The archive is too large when expanded.");
                var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                var hasWindowsReparsePoint = (entry.ExternalAttributes & 0x400) != 0;
                if (unixType == 0xA000 || hasWindowsReparsePoint)
                    throw new InvalidOperationException("Links and reparse points are not allowed in mod archives.");
                if (entry.Length >= CompressionRatioCheckThreshold
                    && (entry.CompressedLength == 0
                        || entry.Length / Math.Max(1, entry.CompressedLength) > MaximumCompressionRatio))
                    throw new InvalidOperationException("The archive has an unsafe compression ratio.");
                ValidateRelativePath(Normalize(entry.FullName));
            }
            return zip;
        }
        catch
        {
            zip.Dispose();
            throw;
        }
    }

    private static ArchiveDescriptor ReadDescriptor(ZipArchive zip)
    {
        var infoEntry = zip.Entries
            .Where(entry => EntryFileName(entry).Equals("Info.json", StringComparison.OrdinalIgnoreCase) && !IsJunk(entry))
            .OrderBy(entry => Normalize(entry.FullName).Count(character => character == '/'))
            .ThenBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (infoEntry is null)
            throw new InvalidOperationException("No Info.json was found in the ZIP archive.");

        using var reader = new StreamReader(infoEntry.Open());
        using var doc = JsonDocument.Parse(reader.ReadToEnd());
        var id = GetString(doc.RootElement, "Id");
        ValidateModId(id);
        var displayName = GetString(doc.RootElement, "DisplayName");
        if (string.IsNullOrWhiteSpace(displayName)) displayName = id;
        var version = GetString(doc.RootElement, "Version");
        return new ArchiveDescriptor(id, displayName, version,
            DirectoryOf(Normalize(infoEntry.FullName)));
    }

    private static int ExtractMod(ZipArchive zip, string sourceDirectory, string staging)
    {
        var prefix = sourceDirectory.Length == 0 ? "" : sourceDirectory.TrimEnd('/') + "/";
        var count = 0;
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) || IsJunk(entry))
                continue;
            var fullName = Normalize(entry.FullName);
            if (prefix.Length > 0 && !fullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var relative = prefix.Length > 0 ? fullName[prefix.Length..] : fullName;
            ValidateRelativePath(relative);
            var destination = Path.GetFullPath(Path.Combine(staging, relative));
            var stagingRoot = Path.GetFullPath(staging).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(stagingRoot, StringComparison.Ordinal))
                throw new InvalidOperationException("The archive contains an unsafe path.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: false);
            count++;
        }
        if (count == 0 || FindInfoJson(staging) is null)
            throw new InvalidOperationException("The archive does not contain a usable mod.");
        return count;
    }

    private static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (Path.IsPathRooted(path) || path.StartsWith('/') || path.Contains('\0'))
            throw new InvalidOperationException("The archive contains an unsafe path.");
        foreach (var component in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            if (component is "." or "..")
                throw new InvalidOperationException("The archive contains an unsafe path.");
    }

    private static void ValidateModId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 160 || id is "." or ".."
            || id.Contains('/') || id.Contains('\\') || id.Contains('\0'))
            throw new InvalidOperationException("Info.json contains an invalid mod Id.");
    }

    private static void MoveDirectory(string src, string dest)
    {
        try
        {
            Directory.Move(src, dest);
        }
        catch (IOException)
        {
            // Cross-volume (e.g. Steam library on another drive): copy then delete.
            CopyDirectory(src, dest);
            Directory.Delete(src, recursive: true);
        }
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(dest, Path.GetRelativePath(src, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string EntryFileName(ZipArchiveEntry entry)
    {
        var name = Normalize(entry.FullName).TrimEnd('/');
        var slash = name.LastIndexOf('/');
        return slash >= 0 ? name[(slash + 1)..] : name;
    }

    private static string DirectoryOf(string normalizedFullName)
    {
        var slash = normalizedFullName.LastIndexOf('/');
        return slash >= 0 ? normalizedFullName[..slash] : "";
    }

    private static bool IsJunk(ZipArchiveEntry entry)
    {
        var full = Normalize(entry.FullName);
        var name = EntryFileName(entry);
        return full.StartsWith("__MACOSX/", StringComparison.Ordinal)
            || name.StartsWith("._", StringComparison.Ordinal)
            || name == ".DS_Store";
    }

    private static string? FindInfoJson(string dir)
    {
        var direct = Path.Combine(dir, "Info.json");
        if (File.Exists(direct))
            return direct;

        return Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => Path.GetFileName(f).Equals("Info.json", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetString(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
