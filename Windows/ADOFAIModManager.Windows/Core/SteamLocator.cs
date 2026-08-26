using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace NativeUmm;

internal static partial class SteamLocator
{
    private const string AppId = "977950";
    private const string GameDirectoryName = "A Dance of Fire and Ice";

    public static IReadOnlyList<string> FindSteamLibraries()
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in CandidateSteamRoots())
        {
            if (!Directory.Exists(root))
                continue;

            libraries.Add(Path.GetFullPath(root));
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                continue;

            try
            {
                foreach (Match match in LibraryPathRegex().Matches(File.ReadAllText(vdf)))
                {
                    var value = match.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(value))
                        libraries.Add(Path.GetFullPath(value));
                }
            }
            catch
            {
                // A malformed optional Steam configuration should not prevent manual selection.
            }
        }
        return libraries.ToList();
    }

    public static string? FindGame()
    {
        foreach (var library in FindSteamLibraries())
        {
            var steamApps = Path.Combine(library, "steamapps");
            var manifest = Path.Combine(steamApps, $"appmanifest_{AppId}.acf");
            var game = Path.Combine(steamApps, "common", GameDirectoryName);
            if (File.Exists(manifest) && GameLayout.IsValidGameDirectory(game))
                return game;
        }

        foreach (var library in FindSteamLibraries())
        {
            var game = Path.Combine(library, "steamapps", "common", GameDirectoryName);
            if (GameLayout.IsValidGameDirectory(game))
                return game;
        }
        return null;
    }

    private static IEnumerable<string> CandidateSteamRoots()
    {
        var hkcu = ReadRegistry(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        if (!string.IsNullOrWhiteSpace(hkcu))
            yield return hkcu;

        var hklm = ReadRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath")
                   ?? ReadRegistry(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
        if (!string.IsNullOrWhiteSpace(hklm))
            yield return hklm;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFiles))
            yield return Path.Combine(programFiles, "Steam");
    }

    private static string? ReadRegistry(RegistryKey root, string keyPath, string name)
    {
        try
        {
            using var key = root.OpenSubKey(keyPath);
            return key?.GetValue(name) as string;
        }
        catch
        {
            return null;
        }
    }

    [GeneratedRegex("\\\"path\\\"\\s*\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();
}
