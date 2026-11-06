using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using NativeUmm.Domain.Games;

namespace NativeUmm.Infrastructure.Installation;

internal enum DoorstopLaunch
{
    Unknown,
    Direct,
    Doorstop,
    Overridden
}

/// <summary>Reads launch metadata for the selected Steam installation without running any commands.</summary>
internal static class SteamLaunchProbe
{
    private const string AppId = "977950";

    public static DoorstopLaunch Read(GameInstallation layout, IReadOnlyList<string>? steamClientRoots = null)
    {
        try
        {
            var library = GameLibrary(layout.GameRoot);
            if (library is null)
                return DoorstopLaunch.Unknown;

            // A secondary library belongs to a client only when that client registers it.
            // Do not let another installed Steam client or another copy of the game win.
            var clients = (steamClientRoots ?? DiscoverClientRoots())
                .Where(Directory.Exists)
                .Select(CanonicalPath)
                .Distinct(PathComparer)
                .Where(client => OwnsLibrary(client, library))
                .ToArray();
            if (clients.Length != 1)
                return DoorstopLaunch.Unknown;

            var user = SelectedUser(clients[0]);
            if (user is null)
                return DoorstopLaunch.Unknown;
            var config = ReadVdf(Path.Combine(clients[0], "userdata", user, "config", "localconfig.vdf"));
            var store = config?.Object("UserLocalConfigStore");
            if (store is null)
                return DoorstopLaunch.Unknown;

            var current = store;
            foreach (var part in new[] { "Software", "Valve", "Steam", "apps", AppId })
            {
                if (!current.Entries!.TryGetValue(part, out var next))
                    return DoorstopLaunch.Direct;
                if (next.Entries is null)
                    return DoorstopLaunch.Unknown;
                current = next;
            }

            if (!current.Entries!.TryGetValue("LaunchOptions", out var options))
                return DoorstopLaunch.Direct;
            return options.Value is string command
                ? ReadCommand(command, layout.GameRoot)
                : DoorstopLaunch.Unknown;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return DoorstopLaunch.Unknown;
        }
    }

    private static string? GameLibrary(string gameRoot)
    {
        // Keep the manifest's lexical library location: an individual installed game may be a symlink.
        var game = new DirectoryInfo(Path.GetFullPath(gameRoot));
        var common = game.Parent;
        var steamapps = common?.Parent;
        if (common?.Name != "common" || steamapps?.Name != "steamapps")
            return null;
        var state = ReadVdf(Path.Combine(steamapps.FullName, "appmanifest_" + AppId + ".acf"))?.Object("AppState");
        var installDirectory = state?.Text("installdir");
        if (state?.Text("appid") != AppId || string.IsNullOrWhiteSpace(installDirectory) ||
            installDirectory is "." or ".." || installDirectory.IndexOfAny(['/', '\\']) >= 0 ||
            Path.IsPathRooted(installDirectory) ||
            !SamePath(game.FullName, Path.Combine(common.FullName, installDirectory)))
            return null;
        return steamapps.Parent?.FullName;
    }

    private static bool OwnsLibrary(string client, string library)
    {
        if (SamePath(client, library))
            return true;
        foreach (var folder in new[] { "steamapps", "config" })
        {
            var libraries = ReadVdf(Path.Combine(client, folder, "libraryfolders.vdf"))?.Object("libraryfolders");
            if (libraries?.Entries is null)
                continue;
            foreach (var (index, entry) in libraries.Entries)
            {
                if (!uint.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    continue;
                // Current clients use objects with a path; older clients used numeric string values.
                var path = entry.Value ?? entry.Text("path");
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && SamePath(path, library))
                    return true;
            }
        }
        return false;
    }

    private static string? SelectedUser(string client)
    {
        var logins = Path.Combine(client, "config", "loginusers.vdf");
        if (File.Exists(logins))
        {
            var users = ReadVdf(logins)?.Object("users");
            if (users?.Entries is null)
                return null;
            var selected = users.Entries.Where(pair => pair.Value.Text("MostRecent") == "1").ToArray();
            if (selected.Length != 1 ||
                !ulong.TryParse(selected[0].Key, NumberStyles.None, CultureInfo.InvariantCulture, out var steamId))
                return null;
            // loginusers keys are SteamID64s; userdata directories use the low 32-bit account ID.
            var account = unchecked((uint)steamId);
            return account == 0 ? null : account.ToString(CultureInfo.InvariantCulture);
        }

        // Older/minimal clients may lack loginusers. A single account directory is unambiguous;
        // multiple cached accounts must never be combined into one effective launch setting.
        var userdata = Path.Combine(client, "userdata");
        if (!Directory.Exists(userdata))
            return null;
        var accounts = Directory.EnumerateDirectories(userdata)
            .Select(Path.GetFileName)
            .Where(name => uint.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id != 0)
            .ToArray();
        return accounts.Length == 1 ? accounts[0] : null;
    }

    internal static DoorstopLaunch ReadCommand(string command, string gameRoot) => ReadCommand(command, gameRoot, 0);

    private static DoorstopLaunch ReadCommand(string command, string gameRoot, int depth)
    {
        if (string.IsNullOrWhiteSpace(command))
            return DoorstopLaunch.Direct;
        if (depth > 4 || !TryShellWords(command, out var words))
            return DoorstopLaunch.Unknown;
        if (words.Count == 0)
            return DoorstopLaunch.Direct;

        var index = 0;
        if (!SkipAssignments(words, ref index))
            return DoorstopLaunch.Unknown;
        if (index >= words.Count)
            return DoorstopLaunch.Direct;
        if (words[index].Text == "exec")
        {
            index++;
            if (index < words.Count && words[index].Text == "--")
                index++;
            else if (index < words.Count && words[index].Text.StartsWith('-'))
                return DoorstopLaunch.Unknown;
        }
        if (!SkipAssignments(words, ref index))
            return DoorstopLaunch.Unknown;
        if (index >= words.Count)
            return DoorstopLaunch.Direct;

        if (IsSystemCommand(words[index].Text, "env"))
        {
            index++;
            while (index < words.Count)
            {
                var option = words[index].Text;
                if (option == "--")
                    index++;
                else if (option.StartsWith('-'))
                    return DoorstopLaunch.Unknown;
                else
                    break;
            }
            if (!SkipAssignments(words, ref index))
                return DoorstopLaunch.Unknown;
        }

        if (index >= words.Count)
            return DoorstopLaunch.Direct;
        if (words[index].Dynamic)
            return DoorstopLaunch.Unknown;

        var executable = words[index++].Text;
        var name = ExecutableName(executable);
        var throughShell = (name is "sh" or "bash" or "dash" or "zsh") && IsSystemCommand(executable, name);
        if (throughShell)
        {
            while (index < words.Count && words[index].Text.StartsWith('-'))
            {
                var option = words[index++].Text;
                if (option == "--")
                    break;
                if (option.StartsWith("--", StringComparison.Ordinal))
                    return DoorstopLaunch.Unknown;
                if (option.Contains('c'))
                    return index < words.Count && !words[index].Dynamic
                        ? ReadCommand(words[index].Text, gameRoot, depth + 1)
                        : DoorstopLaunch.Unknown;
                if (option[1..].Any(character => character is not ('e' or 'u' or 'x' or 'v' or 'f' or 'l')))
                    return DoorstopLaunch.Unknown;
            }
            if (index >= words.Count)
                return DoorstopLaunch.Direct;
            if (words[index].Dynamic)
                return DoorstopLaunch.Unknown;
            executable = words[index].Text;
        }

        if (executable == "%command%")
            return DoorstopLaunch.Direct;
        if (!throughShell && !executable.Contains('/') && !Path.IsPathRooted(executable))
            return DoorstopLaunch.Unknown;
        try
        {
            var path = Path.IsPathRooted(executable) ? executable : Path.Combine(gameRoot, executable);
            if (SamePath(path, Path.Combine(gameRoot, "run.sh")))
            {
                if (words.Skip(index).Any(word => word.Text.StartsWith("--doorstop-", StringComparison.Ordinal) ||
                    word.Text.StartsWith("--doorstop_", StringComparison.Ordinal)))
                    return DoorstopLaunch.Overridden;
                return DoorstopLaunch.Doorstop;
            }
            return IsNativeGame(path, gameRoot) ? DoorstopLaunch.Direct : DoorstopLaunch.Unknown;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return DoorstopLaunch.Unknown;
        }
    }

    private static bool SkipAssignments(IReadOnlyList<ShellWord> words, ref int index)
    {
        while (index < words.Count)
        {
            var text = words[index].Text;
            var equals = text.IndexOf('=');
            if (equals < 1 || !(char.IsAsciiLetter(text[0]) || text[0] == '_') ||
                text[1..equals].Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '_')))
                return true;
            var name = text[..equals];
            if (words[index].Dynamic || name.StartsWith("LD_", StringComparison.Ordinal) ||
                name.StartsWith("DYLD_", StringComparison.Ordinal) || name.StartsWith("DOORSTOP_", StringComparison.Ordinal))
                return false;
            index++;
        }
        return true;
    }

    private static string ExecutableName(string path) => path[(path.LastIndexOf('/') + 1)..];

    private static bool IsSystemCommand(string executable, string name) =>
        executable == name || executable == "/bin/" + name || executable == "/usr/bin/" + name;

    private static bool IsNativeGame(string path, string gameRoot)
    {
        var relative = Path.GetRelativePath(CanonicalPath(gameRoot), CanonicalPath(path)).Replace('\\', '/');
        var parts = relative.Split('/');
        var inRoot = parts.Length == 1;
        var inApp = parts.Length == 4 && parts[0].EndsWith(".app", StringComparison.Ordinal) &&
                    parts[1] == "Contents" && parts[2] == "MacOS";
        var name = parts[^1];
        if ((!inRoot && !inApp) || name is not ("ADanceOfFireAndIce" or "ADanceOfFireAndIce.x86_64" or
            "ADanceOfFireAndIce.real" or "ADanceOfFireAndIce.x86_64.real" or "A Dance of Fire and Ice.exe"))
            return false;
        if (!File.Exists(path))
            return false;
        Span<byte> header = stackalloc byte[4];
        using var file = File.OpenRead(path);
        if (file.Read(header) != header.Length)
            return false;
        var magic = BinaryPrimitives.ReadUInt32BigEndian(header);
        return magic == 0x7f454c46 || // ELF
               header[0] == 'M' && header[1] == 'Z' || // PE
               magic is 0xfeedface or 0xcefaedfe or 0xfeedfacf or 0xcffaedfe or
                   0xcafebabe or 0xbebafeca or 0xcafebabf or 0xbfbafeca; // Mach-O
    }

    private readonly record struct ShellWord(string Text, bool Dynamic);

    private static bool TryShellWords(string command, out List<ShellWord> words)
    {
        words = [];
        var text = new StringBuilder();
        var quoted = '\0';
        var started = false;
        var dynamic = false;
        for (var index = 0; index < command.Length; index++)
        {
            var current = command[index];
            if (quoted == '\'')
            {
                if (current == '\'') quoted = '\0';
                else text.Append(current);
                continue;
            }
            if (current == '\\' && index + 1 < command.Length)
            {
                var next = command[index + 1];
                if (quoted != '"' || next is '"' or '\\' or '$' or '`' or '\n')
                {
                    index++;
                    if (next != '\n') text.Append(next);
                    started = true;
                    continue;
                }
            }
            if (quoted == '"')
            {
                if (current == '"') quoted = '\0';
                else
                {
                    dynamic |= current is '$' or '`';
                    text.Append(current);
                }
                continue;
            }
            if (current is '\'' or '"')
            {
                quoted = current;
                started = true;
            }
            else if (current == '#' && !started)
                break;
            else if (current is ';' or '&' or '|' or '<' or '>' or '(' or ')' or '\n' or '\r')
                return false;
            else if (char.IsWhiteSpace(current))
            {
                if (started)
                {
                    words.Add(new(text.ToString(), dynamic));
                    text.Clear();
                    started = dynamic = false;
                }
            }
            else
            {
                dynamic |= current is '$' or '`';
                text.Append(current);
                started = true;
            }
        }
        if (quoted != '\0')
            return false;
        if (started)
            words.Add(new(text.ToString(), dynamic));
        return true;
    }

    private static IReadOnlyList<string> DiscoverClientRoots()
    {
        var roots = new List<string>();
        void Add(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
                roots.Add(path);
        }
        Add(Environment.GetEnvironmentVariable("STEAM_COMPAT_CLIENT_INSTALL_PATH"));
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            Add(Path.Combine(home, ".steam", "root"));
            Add(Path.Combine(home, ".steam", "steam"));
            Add(Path.Combine(home, ".local", "share", "Steam"));
            Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
            Add(Path.Combine(home, "Library", "Application Support", "Steam"));
        }
        if (OperatingSystem.IsWindows())
            AddWindowsRoots(Add);
        return roots;
    }

    [SupportedOSPlatform("windows")]
    private static void AddWindowsRoots(Action<string?> add)
    {
        foreach (var (hive, keyPath, valueName) in new[]
        {
            (Microsoft.Win32.Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
            (Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
            (Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath")
        })
        {
            try
            {
                using var key = hive.OpenSubKey(keyPath);
                if (key?.GetValue(valueName) is string path)
                    add(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // Well-known client locations remain available when registry discovery is denied.
            }
        }
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(programs))
            add(Path.Combine(programs, "Steam"));
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static bool SamePath(string left, string right) => PathComparer.Equals(CanonicalPath(left), CanonicalPath(right));

    private static string CanonicalPath(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            var directory = new DirectoryInfo(current);
            if (directory.LinkTarget is not null)
                current = directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? current;
        }
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(current));
    }

    private sealed class VdfNode(string? value, Dictionary<string, VdfNode>? entries)
    {
        public string? Value { get; } = value;
        public Dictionary<string, VdfNode>? Entries { get; } = entries;
        public VdfNode? Object(string key) => Entries?.GetValueOrDefault(key) is { Entries: not null } child ? child : null;
        public string? Text(string key) => Entries?.GetValueOrDefault(key)?.Value;
    }

    private static VdfNode? ReadVdf(string path)
    {
        if (!File.Exists(path))
            return null;
        var text = File.ReadAllText(path);
        var index = 0;
        bool SkipSpace()
        {
            while (index < text.Length)
            {
                if (char.IsWhiteSpace(text[index])) index++;
                else if (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '/')
                {
                    while (index < text.Length && text[index] != '\n') index++;
                }
                else return true;
            }
            return false;
        }
        string? ReadText()
        {
            if (!SkipSpace() || text[index] is '{' or '}')
                return null;
            var result = new StringBuilder();
            if (text[index] != '"')
            {
                while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] is not ('{' or '}'))
                    result.Append(text[index++]);
                return result.ToString();
            }
            index++;
            while (index < text.Length)
            {
                var current = text[index++];
                if (current == '"') return result.ToString();
                if (current == '\\' && index < text.Length && text[index] is '\\' or '"')
                    current = text[index++];
                result.Append(current);
            }
            return null;
        }
        VdfNode? ReadObject(int depth, bool nested)
        {
            if (depth > 64) return null;
            var entries = new Dictionary<string, VdfNode>(StringComparer.OrdinalIgnoreCase);
            while (SkipSpace())
            {
                if (text[index] == '}')
                {
                    index++;
                    return nested ? new(null, entries) : null;
                }
                var key = ReadText();
                if (key is null || !SkipSpace()) return null;
                VdfNode? value;
                if (text[index] == '{')
                {
                    index++;
                    value = ReadObject(depth + 1, nested: true);
                }
                else
                {
                    var scalar = ReadText();
                    value = scalar is null ? null : new(scalar, null);
                }
                if (value is null || !entries.TryAdd(key, value)) return null;
            }
            return nested ? null : new(null, entries);
        }
        return ReadObject(0, nested: false);
    }
}
