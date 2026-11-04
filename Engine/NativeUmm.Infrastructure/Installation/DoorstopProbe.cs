using System.Text;
using NativeUmm.Domain.Games;

namespace NativeUmm.Infrastructure.Installation;

/// <summary>
/// UnityDoorstop 4.x layout, taken from the 4.6.0 release archives and config loader:
/// Windows (including Proton) ships winhttp.dll, doorstop_config.ini, and .doorstop_version.
/// The ini is [General] enabled / target_assembly. load_bool_file accepts only true or false
/// and defaults a missing enabled key to true (src/windows/config.c). GetPrivateProfileString
/// drops one pair of matching surrounding quotes.
/// Linux ships run.sh and libdoorstop.so. macOS ships run.sh and libdoorstop.dylib.
/// run.sh assigns enabled and target_assembly, then exports DOORSTOP_ENABLED and
/// DOORSTOP_TARGET_ASSEMBLY. The Unix loader enables Doorstop only when that value is "1"
/// (src/nix/config.c). Those files do not inject unless the game is started through run.sh
/// or a preload wrapper; launching the binary or .app directly skips Doorstop.
/// Relative targets resolve from the game root. On a Unix host, a Windows drive path such
/// as Z:\... is resolved through the game's Proton dosdevices link, or Z: → / when Wine's
/// default root mapping is the only one available.
/// </summary>
internal static class DoorstopProbe
{
    internal readonly record struct Inspection(bool Active, bool OwnedByUmm, IReadOnlyList<string> Files)
    {
        public static Inspection None { get; } = new(false, false, []);
    }

    public static Inspection Inspect(GameInstallation layout)
    {
        try
        {
            return InspectCore(layout);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Inspection.None;
        }
    }

    private static Inspection InspectCore(GameInstallation layout)
    {
        var runtime = RuntimeOf(layout);
        var configPath = Path.Combine(layout.GameRoot, runtime == GameRuntime.Windows ? "doorstop_config.ini" : "run.sh");
        if (!File.Exists(configPath))
            return Inspection.None;

        if (!TryReadConfig(runtime, configPath, out var enabled, out var target))
            return Inspection.None;

        var expected = Path.GetFullPath(Path.Combine(layout.ManagerPath, "UnityModManager.dll"));
        var resolved = Resolve(layout, runtime, target);
        if (resolved is null || !SamePath(resolved, expected, runtime))
            return Inspection.None;

        var files = new List<string> { configPath };
        var library = Path.Combine(layout.GameRoot, LibraryName(runtime));
        if (File.Exists(library))
            files.Add(library);
        var version = Path.Combine(layout.GameRoot, ".doorstop_version");
        if (File.Exists(version))
            files.Add(version);

        // winhttp.dll injects every time the Windows executable starts. On Linux and
        // macOS the library is only preloaded when a launcher actually runs run.sh.
        var launched = runtime == GameRuntime.Windows || UnixLaunchUsesDoorstop(layout, runtime);
        return new Inspection(enabled && File.Exists(library) && launched, OwnedByUmm: true, files);
    }

    internal static GameRuntime RuntimeOf(GameInstallation layout)
    {
        var app = layout.AppPath;
        if (app.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return GameRuntime.Windows;
        if (app.EndsWith(".app", StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(Path.Combine(app, "Contents", "MacOS")))
            return GameRuntime.Mac;
        return GameRuntime.Linux;
    }

    private static string LibraryName(GameRuntime runtime) => runtime switch
    {
        GameRuntime.Windows => "winhttp.dll",
        GameRuntime.Mac => "libdoorstop.dylib",
        _ => "libdoorstop.so"
    };

    private static bool TryReadConfig(GameRuntime runtime, string path, out bool enabled, out string target)
    {
        if (runtime == GameRuntime.Windows)
            return TryReadIni(path, out enabled, out target);
        return TryReadScript(path, out enabled, out target);
    }

    private static bool TryReadIni(string path, out bool enabled, out string target)
    {
        string? enabledValue = null;
        string? targetValue = null;
        var inGeneral = false;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or ';')
                continue;
            if (line[0] == '[')
            {
                var end = line.IndexOf(']');
                var name = end > 1 ? line[1..end].Trim() : "";
                inGeneral = name.Equals("General", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inGeneral)
                continue;
            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            var key = line[..eq].Trim();
            // GetPrivateProfileString discards one matching pair of surrounding quotes.
            var value = StripProfileQuotes(line[(eq + 1)..].Trim());
            // The profile API returns the first matching key.
            if (enabledValue is null && key.Equals("enabled", StringComparison.OrdinalIgnoreCase))
                enabledValue = value;
            else if (targetValue is null && key.Equals("target_assembly", StringComparison.OrdinalIgnoreCase))
                targetValue = value;
        }

        // Missing key defaults to true. Any value other than true, including false and 1, is off.
        enabled = enabledValue is null || enabledValue.Equals("true", StringComparison.OrdinalIgnoreCase);
        // A missing target_assembly falls back to Doorstop.dll, which is not UMM.
        target = string.IsNullOrWhiteSpace(targetValue) ? "Doorstop.dll" : targetValue;
        return true;
    }

    private static bool TryReadScript(string path, out bool enabled, out string target)
    {
        string? enabledValue = null;
        string? targetValue = null;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            // Shell assignments are last-wins. Ignore command substitutions such as
            // enabled="$(doorstop_bool "$2")", which override the file only at launch.
            if (TryAssignment(line, "enabled", out var enabledText) && IsLiteral(enabledText))
                enabledValue = enabledText;
            if (TryAssignment(line, "target_assembly", out var targetText) && IsLiteral(targetText))
                targetValue = targetText;
        }

        enabled = enabledValue == "1";
        target = targetValue ?? "";
        return target.Length > 0;
    }

    private static bool TryAssignment(string line, string name, out string value)
    {
        value = "";
        var text = line;
        if (text.StartsWith("export ", StringComparison.Ordinal))
            text = text["export ".Length..].TrimStart();
        if (!text.StartsWith(name, StringComparison.Ordinal))
            return false;
        var after = text[name.Length..].TrimStart();
        if (after.Length == 0 || after[0] != '=')
            return false;
        value = Unquote(after[1..].TrimStart());
        return true;
    }

    private static bool IsLiteral(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0 && !trimmed.StartsWith('$') && !trimmed.Contains("$(") && !trimmed.Contains("${");
    }

    private static string Unquote(string raw)
    {
        if (raw.Length >= 2 && raw[0] is '"' or '\'')
        {
            var quote = raw[0];
            var end = raw.IndexOf(quote, 1);
            if (end >= 1)
                return raw[1..end];
        }

        var cut = raw.IndexOfAny([' ', '\t', '#']);
        return cut >= 0 ? raw[..cut] : raw;
    }

    private static string StripProfileQuotes(string value)
    {
        if (value.Length >= 2 && value[0] == value[^1] && value[0] is '"' or '\'')
            return value[1..^1];
        return value;
    }

    private static string? Resolve(GameInstallation layout, GameRuntime runtime, string configured)
    {
        var text = configured.Trim();
        if (text.Length == 0)
            return null;

        if (runtime == GameRuntime.Windows && TryWindowsDrive(text, out var drive, out var rest))
        {
            if (OperatingSystem.IsWindows())
                return Path.GetFullPath(text);

            var root = MapWineDrive(layout.GameRoot, drive);
            if (root is null)
                return null;
            var relative = rest.Replace('\\', '/').TrimStart('/');
            var combined = relative.Length == 0 ? root : Path.Combine(root, relative);
            return Path.GetFullPath(combined);
        }

        var normalized = text.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var path = Path.IsPathRooted(normalized) ? normalized : Path.Combine(layout.GameRoot, normalized);
        return Path.GetFullPath(path);
    }

    private static bool TryWindowsDrive(string text, out char drive, out string rest)
    {
        drive = '\0';
        rest = "";
        if (text.Length < 3 || text[1] != ':' || text[2] is not ('\\' or '/'))
            return false;
        if (text[0] is not ((>= 'A' and <= 'Z') or (>= 'a' and <= 'z')))
            return false;
        drive = text[0];
        rest = text[3..];
        return true;
    }

    private static string? MapWineDrive(string gameRoot, char drive)
    {
        var steamapps = SteamDirectory(gameRoot, "steamapps");
        if (steamapps is not null)
        {
            var dosdevices = Path.Combine(steamapps, "compatdata", "977950", "pfx", "dosdevices");
            if (Directory.Exists(dosdevices))
            {
                foreach (var name in new[] { char.ToLowerInvariant(drive) + ":", char.ToUpperInvariant(drive) + ":" })
                {
                    var mapped = ResolveDriveLink(Path.Combine(dosdevices, name));
                    if (mapped is not null)
                        return mapped;
                }
            }
        }

        // Wine creates Z: → / when a prefix has no other root mapping.
        return char.ToUpperInvariant(drive) == 'Z' ? "/" : null;
    }

    private static string? ResolveDriveLink(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return null;
        FileSystemInfo link = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        var target = link.ResolveLinkTarget(returnFinalTarget: true);
        if (target is not null)
            return target.FullName;
        return Directory.Exists(path) ? Path.GetFullPath(path) : null;
    }

    private static bool UnixLaunchUsesDoorstop(GameInstallation layout, GameRuntime runtime)
    {
        if (SteamLaunchOptions(layout.GameRoot) is string options &&
            options.Contains("run.sh", StringComparison.Ordinal))
            return true;
        if (DesktopFileLaunches(layout.GameRoot))
            return true;
        return ExecutablePreloadsDoorstop(layout, runtime);
    }

    private static string? SteamLaunchOptions(string gameRoot)
    {
        try
        {
            var steam = SteamDirectory(gameRoot, "steamapps");
            var root = steam is null ? null : Directory.GetParent(steam)?.FullName;
            var userdata = root is null ? null : Path.Combine(root, "userdata");
            if (userdata is null || !Directory.Exists(userdata))
                return null;
            foreach (var user in Directory.EnumerateDirectories(userdata))
            {
                var config = Path.Combine(user, "config", "localconfig.vdf");
                if (!File.Exists(config))
                    continue;
                var options = AppLaunchOptions(File.ReadAllText(config));
                if (options is not null && options.Contains("run.sh", StringComparison.Ordinal))
                    return options;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        return null;
    }

    private static string? AppLaunchOptions(string text)
    {
        var tokens = VdfTokens(text).ToArray();
        var index = 0;
        string? found = null;
        bool Read(string prefix)
        {
            while (index < tokens.Length)
            {
                var key = tokens[index++];
                if (key == "}")
                    return true;
                if (key == "{" || index >= tokens.Length)
                    return false;
                var name = UnquoteVdf(key);
                var value = tokens[index++];
                if (value == "{")
                {
                    if (!Read(prefix + name + "/"))
                        return false;
                }
                else if (value != "}" &&
                         name.Equals("LaunchOptions", StringComparison.OrdinalIgnoreCase) &&
                         prefix.EndsWith("apps/977950/", StringComparison.OrdinalIgnoreCase))
                {
                    found = UnquoteVdf(value);
                }
            }
            return true;
        }
        Read("");
        return found;
    }

    private static IEnumerable<string> VdfTokens(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            var current = text[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }
            if (current is '{' or '}')
            {
                yield return current.ToString();
                index++;
                continue;
            }
            if (current != '"')
            {
                index++;
                continue;
            }
            var start = index++;
            while (index < text.Length)
            {
                if (text[index] == '\\')
                {
                    index = Math.Min(text.Length, index + 2);
                    continue;
                }
                if (text[index] == '"')
                {
                    index++;
                    break;
                }
                index++;
            }
            yield return text[start..index];
        }
    }

    private static string UnquoteVdf(string token)
    {
        if (token.Length < 2 || token[0] != '"' || token[^1] != '"')
            return token;
        return token[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static bool DesktopFileLaunches(string gameRoot)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(gameRoot, "*.desktop");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        foreach (var file in files)
        {
            try
            {
                foreach (var raw in File.ReadLines(file))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("Exec=", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains("run.sh", StringComparison.Ordinal))
                        return true;
                }
            }
            catch (IOException)
            {
            }
        }
        return false;
    }

    private static bool ExecutablePreloadsDoorstop(GameInstallation layout, GameRuntime runtime)
    {
        if (Directory.Exists(layout.AppPath) || !File.Exists(layout.AppPath))
            return false;
        try
        {
            var info = new FileInfo(layout.AppPath);
            if (info.Length is < 2 or > 256 * 1024)
                return false;
            using var stream = File.OpenRead(layout.AppPath);
            Span<byte> magic = stackalloc byte[2];
            if (stream.Read(magic) != 2 || magic[0] != (byte)'#' || magic[1] != (byte)'!')
                return false;
            var text = File.ReadAllText(layout.AppPath, Encoding.UTF8);
            var preload = runtime == GameRuntime.Mac ? "DYLD_INSERT_LIBRARIES" : "LD_PRELOAD";
            return text.Contains(preload, StringComparison.Ordinal) &&
                   text.Contains(LibraryName(runtime), StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? SteamDirectory(string gameRoot, string name)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(gameRoot));
        while (dir is not null)
        {
            if (dir.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static bool SamePath(string left, string right, GameRuntime runtime)
    {
        var comparison = runtime == GameRuntime.Linux
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        return string.Equals(left, right, comparison);
    }
}

internal enum GameRuntime
{
    Windows,
    Mac,
    Linux
}
