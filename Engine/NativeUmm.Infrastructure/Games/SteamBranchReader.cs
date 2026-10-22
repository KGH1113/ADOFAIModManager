using System.Text.RegularExpressions;

namespace NativeUmm.Infrastructure.Games;

public sealed record SteamBranchStatus(string? Branch, bool AlphaInstalled);

public static partial class SteamBranchReader
{
    // Read the manifest beside the selected game's library, never another Steam installation.
    public static SteamBranchStatus Read(string gameRoot)
    {
        try
        {
            var game = new DirectoryInfo(Path.GetFullPath(gameRoot));
            var common = game.Parent;
            var steamapps = common?.Parent;
            if (steamapps is null || common?.Name != "common" || !string.Equals(steamapps.Name, "steamapps", StringComparison.OrdinalIgnoreCase))
                return new(null, false);
            var manifest = Path.Combine(steamapps.FullName, "appmanifest_977950.acf");
            if (!File.Exists(manifest)) return new(null, false);
            return Parse(File.ReadAllText(manifest), game.Name);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new(null, false);
        }
    }

    internal static SteamBranchStatus Parse(string manifest, string installDirectory)
    {
        var tokens = Token().Matches(manifest).Select(match => match.Value).ToArray();
        var index = 0;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool ReadObject(string prefix, bool nested)
        {
            while (index < tokens.Length)
            {
                var key = tokens[index++];
                if (key == "}") return nested;
                if (key == "{" || index >= tokens.Length) return false;
                key = key[1..^1];
                var value = tokens[index++];
                var path = prefix + key;
                if (value == "{")
                {
                    if (!ReadObject(path + "/", true)) return false;
                }
                else if (value == "}" || !values.TryAdd(path, value[1..^1])) return false;
            }
            return !nested;
        }
        if (!ReadObject("", false)) return new(null, false);
        string? Value(string path) => values.GetValueOrDefault("AppState/" + path);
        if (Value("appid") != "977950" || !string.Equals(Value("installdir"), installDirectory, StringComparison.OrdinalIgnoreCase))
            return new(null, false);

        var selected = Value("UserConfig/BetaKey") ?? "public";
        var mounted = Value("MountedConfig/BetaKey");
        var target = Value("TargetBuildID");
        var build = Value("buildid");
        // UserConfig alone only proves selection. MountedConfig and StateFlags prove completion.
        var complete = Value("StateFlags") == "4" && !string.IsNullOrEmpty(build) &&
            (string.IsNullOrEmpty(target) || target == "0" || target == build);
        return new(selected, complete && string.Equals(selected, "alpha", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(mounted, "alpha", StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex("\"(?:\\\\.|[^\"\\\\])*\"|[{}]")]
    private static partial Regex Token();
}
