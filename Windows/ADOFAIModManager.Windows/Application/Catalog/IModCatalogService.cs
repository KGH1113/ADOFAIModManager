namespace ADOFAIModManager.Windows.Application.Catalog;

internal interface IModCatalogService
{
    Task<IReadOnlyList<RemoteMod>> FetchModsAsync(CancellationToken cancellationToken = default);
    Task<string> DownloadAsync(
        RemoteMod mod,
        IProgress<double?>? progress = null,
        CancellationToken cancellationToken = default);
}

internal enum ModCatalogError
{
    InvalidResponse,
    InsecureUrl,
    FileTooLarge,
    NotZip
}

internal sealed class ModCatalogException(ModCatalogError error) : Exception(error.ToString())
{
    public ModCatalogError Error { get; } = error;
}
