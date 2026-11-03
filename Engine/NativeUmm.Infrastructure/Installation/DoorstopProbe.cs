using NativeUmm.Domain.Games;

namespace NativeUmm.Infrastructure.Installation;

/// <summary>
/// UnityDoorstop 4.x layout, taken from the 4.6.0 release archives and config loader:
/// Windows (including Proton) ships winhttp.dll, doorstop_config.ini, and .doorstop_version.
/// The ini is [General] enabled / target_assembly. load_bool_file accepts only true or false
/// and defaults a missing enabled key to true (src/windows/config.c).
/// Linux ships run.sh and libdoorstop.so. macOS ships run.sh and libdoorstop.dylib.
/// run.sh assigns enabled and target_assembly, then exports DOORSTOP_ENABLED and
/// DOORSTOP_TARGET_ASSEMBLY. The Unix loader enables Doorstop only when that value is "1"
/// (src/nix/config.c). Relative targets resolve from the game root, which is also the
/// directory next to a macOS .app bundle.
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
        var resolved = Resolve(layout.GameRoot, target);
        if (resolved is null || !SamePath(resolved, expected, runtime))
            return Inspection.None;

        var files = new List<string> { configPath };
        var library = Path.Combine(layout.GameRoot, LibraryName(runtime));
        if (File.Exists(library))
            files.Add(library);
        var version = Path.Combine(layout.GameRoot, ".doorstop_version");
        if (File.Exists(version))
            files.Add(version);

        // The native library has to be present; a config alone cannot inject.
        return new Inspection(enabled && File.Exists(library), OwnedByUmm: true, files);
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
            var value = line[(eq + 1)..].Trim();
            // GetPrivateProfileString returns the first matching key.
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

    private static string? Resolve(string gameRoot, string configured)
    {
        var text = configured.Trim();
        if (text.Length == 0)
            return null;
        var normalized = text.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var combined = Path.IsPathRooted(normalized) ? normalized : Path.Combine(gameRoot, normalized);
        return Path.GetFullPath(combined);
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
