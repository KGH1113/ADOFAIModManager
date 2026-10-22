using NativeUmm.Application.Abstractions;

namespace NativeUmm.Infrastructure.Storage;

public sealed class AppDataPaths : IAppDataPaths
{
    private readonly string root;

    public AppDataPaths(string root)
    {
        this.root = root;
    }

    public string Cache => Ensure(Path.Combine(root, "cache"));
    public string RemovedMods => Ensure(Path.Combine(root, "removed-mods"));
    public string RemovedUmm => Ensure(Path.Combine(root, "removed-umm"));

    public static AppDataPaths ForMacOS()
    {
        var overridePath = Environment.GetEnvironmentVariable("ADOFAI_MOD_MANAGER_DATA");
        var root = overridePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "ADOFAI Mod Manager");
        return new AppDataPaths(root);
    }

    public static AppDataPaths ForWindows() => new(Environment.GetEnvironmentVariable("ADOFAI_MOD_MANAGER_DATA") ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ADOFAIModManager", "Data"));

    public static AppDataPaths ForLinux()
    {
        var overridePath = Environment.GetEnvironmentVariable("ADOFAI_MOD_MANAGER_DATA");
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome) || !Path.IsPathRooted(dataHome))
            dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return new AppDataPaths(overridePath ?? Path.Combine(dataHome, "ADOFAIModManager"));
    }

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
