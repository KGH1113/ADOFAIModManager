namespace ADOFAIModManager.Windows.Infrastructure.Catalog;

internal static class CatalogDownloadStorage
{
    private const string DirectoryName = "ADOFAIModManagerCatalogDownloads";
    internal static string DirectoryPath => Path.Combine(Path.GetTempPath(), DirectoryName);

    public static string CreateDestination()
    {
        Directory.CreateDirectory(DirectoryPath);
        return Path.Combine(DirectoryPath, $"{Guid.NewGuid():N}.zip");
    }

    public static void RemoveOwnedFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.Equals(parent, Path.GetFullPath(DirectoryPath), StringComparison.OrdinalIgnoreCase)) return;
        try { File.Delete(path); } catch { }
    }

    public static void CleanupAll()
    {
        try { if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true); }
        catch { }
    }
}
