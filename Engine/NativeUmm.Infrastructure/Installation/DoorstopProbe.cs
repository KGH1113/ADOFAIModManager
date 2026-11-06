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
/// (src/nix/config.c). Only this installation's Steam launch settings or its actual
/// executable wrapper establish an active Unix deployment. A desktop shortcut is
/// not evidence that it is used. Unverified launch paths must be confirmed before
/// another injection method is installed.
/// Relative targets resolve from the game root. On a Unix host, a Windows drive path such
/// as Z:\... is resolved through the game's Proton dosdevices link, or Z: → / when Wine's
/// default root mapping is the only one available.
/// </summary>
internal static class DoorstopProbe
{
    internal const string UnverifiedLaunchWarning =
        "UnityDoorstop is configured for UnityModManager, but this game's Unix launch path could not be verified. " +
        "Use this game's ./run.sh %command% without Doorstop command-line overrides in Steam, " +
        "or disable the unverified Doorstop launcher before installing the assembly hook.";

    internal readonly record struct Inspection(
        bool Active,
        bool OwnedByUmm,
        IReadOnlyList<string> Files,
        bool NeedsLaunchConfirmation = false)
    {
        public static Inspection None { get; } = new(false, false, []);
    }

    public static Inspection Inspect(GameInstallation layout, IReadOnlyList<string>? steamClientRoots = null)
    {
        try
        {
            return InspectCore(layout, steamClientRoots);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Inspection.None;
        }
    }

    private static Inspection InspectCore(GameInstallation layout, IReadOnlyList<string>? steamClientRoots)
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

        var launch = runtime == GameRuntime.Windows
            ? DoorstopLaunch.Doorstop
            : UnixLaunch(layout, runtime, steamClientRoots);
        var configured = enabled && File.Exists(library);
        return new Inspection(
            configured && launch == DoorstopLaunch.Doorstop,
            OwnedByUmm: true,
            files,
            NeedsLaunchConfirmation: File.Exists(library) &&
                ((enabled && launch == DoorstopLaunch.Unknown) || launch == DoorstopLaunch.Overridden));
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

    private static DoorstopLaunch UnixLaunch(
        GameInstallation layout, GameRuntime runtime, IReadOnlyList<string>? steamClientRoots)
    {
        var steam = SteamLaunchProbe.Read(layout, steamClientRoots);
        if (steam is DoorstopLaunch.Doorstop or DoorstopLaunch.Overridden)
            return steam;

        // Only inspect the executable the selected layout actually starts.
        // Arbitrary desktop shortcuts and unrelated Steam installs do not select it.
        var executable = ExecutableLaunch(layout, runtime);
        if (executable == DoorstopLaunch.Overridden)
            return DoorstopLaunch.Overridden;
        if (executable == DoorstopLaunch.Doorstop)
            return DoorstopLaunch.Doorstop;
        // An unverified external launcher can change how the selected executable is
        // started. Its apparent direct path must not override uncertain metadata.
        return steam == DoorstopLaunch.Unknown ? steam : executable ?? steam;
    }

    private static DoorstopLaunch? ExecutableLaunch(GameInstallation layout, GameRuntime runtime)
    {
        if (Directory.Exists(layout.AppPath) || !File.Exists(layout.AppPath))
            return null;
        try
        {
            var info = new FileInfo(layout.AppPath);
            if (info.Length is < 2 or > 256 * 1024)
                return null;
            using var stream = File.OpenRead(layout.AppPath);
            Span<byte> magic = stackalloc byte[2];
            if (stream.Read(magic) != 2 || magic[0] != (byte)'#' || magic[1] != (byte)'!')
                return null;

            var preloadName = runtime == GameRuntime.Mac ? "DYLD_INSERT_LIBRARIES" : "LD_PRELOAD";
            var names = new[] { preloadName, "DOORSTOP_ENABLED", "DOORSTOP_TARGET_ASSEMBLY" };
            var environment = new Dictionary<string, (string Value, bool Exported)>(StringComparer.Ordinal);
            foreach (var raw in File.ReadLines(layout.AppPath, Encoding.UTF8).Skip(1))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                var assignment = false;
                foreach (var name in names)
                {
                    if (TryAssignment(line, name, out var value))
                    {
                        if (!StandaloneWrapperAssignment(line, name))
                            return DoorstopLaunch.Unknown;
                        var exported = line.StartsWith("export ", StringComparison.Ordinal) ||
                            (environment.TryGetValue(name, out var previous) && previous.Exported);
                        environment[name] = (value, exported);
                        assignment = true;
                        break;
                    }
                    if (line == "export " + name)
                    {
                        if (!environment.TryGetValue(name, out var previous))
                            return DoorstopLaunch.Unknown;
                        environment[name] = (previous.Value, true);
                        assignment = true;
                        break;
                    }
                    if (line == "unset " + name)
                    {
                        environment.Remove(name);
                        assignment = true;
                        break;
                    }
                }
                if (assignment)
                    continue;
                if (line.StartsWith("exec ", StringComparison.Ordinal))
                {
                    var command = SteamLaunchProbe.ReadCommand(line["exec ".Length..], layout.GameRoot);
                    if (command != DoorstopLaunch.Direct)
                        return command;
                    if (!environment.TryGetValue(preloadName, out var preload) || !preload.Exported)
                        return DoorstopLaunch.Direct;
                    if (!PreloadsOwnLibrary(preload.Value, layout.GameRoot, runtime))
                        return DoorstopLaunch.Direct;
                    if (!environment.TryGetValue("DOORSTOP_ENABLED", out var enabled) || !enabled.Exported)
                        return DoorstopLaunch.Unknown;
                    if (enabled.Value != "1")
                        return DoorstopLaunch.Direct;
                    if (!environment.TryGetValue("DOORSTOP_TARGET_ASSEMBLY", out var target) || !target.Exported)
                        return DoorstopLaunch.Unknown;
                    var resolved = Resolve(layout, runtime, target.Value);
                    var expected = Path.GetFullPath(Path.Combine(layout.ManagerPath, "UnityModManager.dll"));
                    return resolved is not null && SamePath(resolved, expected, runtime)
                        ? DoorstopLaunch.Doorstop
                        : DoorstopLaunch.Direct;
                }

                // Do not infer shell control flow, substitutions, or side effects.
                return DoorstopLaunch.Unknown;
            }
            return DoorstopLaunch.Unknown;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return DoorstopLaunch.Unknown;
        }
    }

    private static bool StandaloneWrapperAssignment(string line, string name)
    {
        var statement = line.StartsWith("export ", StringComparison.Ordinal)
            ? line["export ".Length..].TrimStart()
            : line;
        if (!statement.StartsWith(name + "=", StringComparison.Ordinal))
            return false;
        var raw = line[(line.IndexOf('=') + 1)..].TrimStart();
        if (raw.IndexOfAny(['$', (char)96, '\\']) >= 0)
            return false;
        if (raw.Length == 0)
            return true;

        int end;
        if (raw[0] is '"' or '\'')
        {
            end = raw.IndexOf(raw[0], 1);
            if (end < 0)
                return false;
            end++;
        }
        else
        {
            end = raw.IndexOfAny([' ', '\t']);
            if (end < 0)
                end = raw.Length;
            if (raw[..end].IndexOfAny([';', '&', '|', '<', '>', '(', ')', '#', '"', '\'']) >= 0)
                return false;
        }
        var suffix = raw[end..].TrimStart();
        return suffix.Length == 0 || suffix[0] == '#';
    }

    private static bool PreloadsOwnLibrary(string value, string gameRoot, GameRuntime runtime)
    {
        var expected = Path.GetFullPath(Path.Combine(gameRoot, LibraryName(runtime)));
        char[] separators = runtime == GameRuntime.Mac ? [':'] : [':', ' ', '\t'];
        return value.Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Any(path => SamePath(Path.GetFullPath(path, gameRoot), expected, runtime));
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
