using System.Text.Json.Serialization;

namespace ADOFAIModManager.Windows.Application.Catalog;

internal enum RemoteModAction
{
    DownloadAndInstall,
    OpenWebsite,
    DownloadAndInspect
}

internal sealed class RemoteMod
{
    private string? id;
    private string? name;
    private string? description;
    private string? cachedUsername;

    [JsonPropertyName("id")] public string Id { get => id ?? ""; init => id = value; }
    [JsonPropertyName("name")] public string Name { get => name ?? ""; init => name = value; }
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("description")] public string Description { get => description ?? ""; init => description = value; }
    [JsonPropertyName("cachedUsername")] public string CachedUsername { get => cachedUsername ?? ""; init => cachedUsername = value; }
    [JsonPropertyName("uploadedTimestamp")] public long UploadedTimestamp { get; init; }
    [JsonPropertyName("parsedDownload")] public string? ParsedDownload { get; init; }
    [JsonPropertyName("download")] public string? Download { get; init; }
    [JsonPropertyName("imageURL")] public string? ImageUrl { get; init; }
    [JsonPropertyName("hideFromSearch")] public bool HideFromSearch { get; init; }

    [JsonIgnore]
    public Uri? PreferredDownloadUri => ParseHttps(ParsedDownload) ?? ParseHttps(Download);

    [JsonIgnore]
    public Uri? ImageUri => ParseHttps(ImageUrl);

    [JsonIgnore]
    public RemoteModAction Action
    {
        get
        {
            var uri = PreferredDownloadUri;
            if (uri is null) return RemoteModAction.OpenWebsite;
            if (Path.GetExtension(uri.AbsolutePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                return RemoteModAction.DownloadAndInstall;

            var host = uri.Host.ToLowerInvariant();
            var path = uri.AbsolutePath.ToLowerInvariant();
            if (host == "youtu.be" || host.EndsWith("youtube.com", StringComparison.Ordinal))
                return RemoteModAction.OpenWebsite;
            if (host == "github.com" && !path.Contains("/releases/download/", StringComparison.Ordinal))
                return RemoteModAction.OpenWebsite;
            if (Path.GetExtension(path) is ".htm" or ".html")
                return RemoteModAction.OpenWebsite;
            return RemoteModAction.DownloadAndInspect;
        }
    }

    public bool Matches(string query)
    {
        query = query.Trim();
        if (query.Length == 0) return true;
        return Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || CachedUsername.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || Description.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private static Uri? ParseHttps(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? uri
            : null;
}
