using System.Buffers;
using System.Net;
using System.Text.Json;
using ADOFAIModManager.Windows.Application.Catalog;

namespace ADOFAIModManager.Windows.Infrastructure.Catalog;

internal sealed class ModCatalogClient : IModCatalogService
{
    internal static readonly Uri Endpoint = new("https://bot.adofai.gg/api/mods/");
    internal const long MaximumDownloadBytes = 512L * 1024 * 1024;
    private readonly HttpClient client;
    private readonly Uri endpoint;
    private readonly long maximumDownloadBytes;

    public ModCatalogClient(HttpClient client, Uri? endpoint = null, long maximumDownloadBytes = MaximumDownloadBytes)
    {
        this.client = client;
        this.endpoint = endpoint ?? Endpoint;
        this.maximumDownloadBytes = maximumDownloadBytes;
    }

    public async Task<IReadOnlyList<RemoteMod>> FetchModsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        ValidateResponse(response);
        if (response.RequestMessage?.RequestUri is not { } finalUri
            || !finalUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new ModCatalogException(ModCatalogError.InsecureUrl);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var mods = await JsonSerializer.DeserializeAsync<List<RemoteMod>>(stream, cancellationToken: cancellationToken)
            ?? [];
        return mods.Where(mod => !mod.HideFromSearch)
            .OrderByDescending(mod => mod.UploadedTimestamp)
            .ToArray();
    }

    public async Task<string> DownloadAsync(
        RemoteMod mod,
        IProgress<double?>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var source = mod.PreferredDownloadUri;
        if (source is null) throw new ModCatalogException(ModCatalogError.InsecureUrl);

        using var response = await client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        ValidateResponse(response);
        if (response.RequestMessage?.RequestUri is not { } finalUri
            || !finalUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new ModCatalogException(ModCatalogError.InsecureUrl);

        var expected = response.Content.Headers.ContentLength;
        if (expected > maximumDownloadBytes)
            throw new ModCatalogException(ModCatalogError.FileTooLarge);

        var destination = CatalogDownloadStorage.CreateDestination();
        try
        {
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                long total = 0;
                while (true)
                {
                    var count = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (count == 0) break;
                    total += count;
                    if (total > maximumDownloadBytes)
                        throw new ModCatalogException(ModCatalogError.FileTooLarge);
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    progress?.Report(expected is > 0 ? (double)total / expected.Value : null);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            await output.FlushAsync(cancellationToken);
            await output.DisposeAsync();
            if (!HasZipSignature(destination))
                throw new ModCatalogException(ModCatalogError.NotZip);
            return destination;
        }
        catch
        {
            CatalogDownloadStorage.RemoveOwnedFile(destination);
            throw;
        }
    }

    private static void ValidateResponse(HttpResponseMessage response)
    {
        if (response.StatusCode < HttpStatusCode.OK || response.StatusCode >= HttpStatusCode.MultipleChoices)
            throw new ModCatalogException(ModCatalogError.InvalidResponse);
    }

    internal static bool HasZipSignature(string path)
    {
        Span<byte> signature = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        if (stream.Read(signature) != signature.Length) return false;
        return signature.SequenceEqual(new byte[] { 0x50, 0x4b, 0x03, 0x04 })
            || signature.SequenceEqual(new byte[] { 0x50, 0x4b, 0x05, 0x06 })
            || signature.SequenceEqual(new byte[] { 0x50, 0x4b, 0x07, 0x08 });
    }
}
