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

    public static AppDataPaths ForWindows() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ADOFAIModManager", "Data"));

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
