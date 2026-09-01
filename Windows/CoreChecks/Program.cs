using System.IO.Compression;
using System.Text;
using ADOFAIModManager.Windows.Infrastructure.Steam;
using NativeUmm.Domain.Games;
using NativeUmm.Domain.Installation;
using NativeUmm.Infrastructure.Installation;
using NativeUmm.Infrastructure.Logging;
using NativeUmm.Infrastructure.Mods;
using NativeUmm.Infrastructure.Storage;
using ADOFAIModManager.Windows.Application.Localization;
using ADOFAIModManager.Windows.Infrastructure.Localization;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using ADOFAIModManager.Windows.Application.Catalog;
using ADOFAIModManager.Windows.Infrastructure.Catalog;

var locator = new WindowsGameLocator();
if (locator.Locate(Path.Combine(Path.GetTempPath(), "adofai-does-not-exist")) is not null)
    throw new InvalidOperationException("Invalid game directory was accepted.");

if (UmmSpecification.EntryPoint.ToConfigString()
        != "[UnityEngine.CoreModule.dll]UnityEngine.MonoBehaviour.cctor:Before"
    || UmmSpecification.StartingPoint.ToConfigString()
        != "[Assembly-CSharp.dll]ADOStartup.Startup:Before"
    || UmmSpecification.UIStartingPoint.ToConfigString()
        != "[Assembly-CSharp.dll]ADOStartup.Startup:After")
    throw new InvalidOperationException("ADOFAI UMM startup points are incorrect.");

var generatedConfig = Installer.BuildGameConfig().Root
    ?? throw new InvalidOperationException("Generated UMM config has no root element.");
if (generatedConfig.Element("EntryPoint")?.Value != UmmSpecification.EntryPoint.ToConfigString()
    || generatedConfig.Element("StartingPoint")?.Value != UmmSpecification.StartingPoint.ToConfigString()
    || generatedConfig.Element("UIStartingPoint")?.Value != UmmSpecification.UIStartingPoint.ToConfigString())
    throw new InvalidOperationException("Generated UMM config does not use the ADOFAI startup points.");

var testRoot = Path.Combine(Path.GetTempPath(), $"adofai-windows-core-checks-{Guid.NewGuid():N}");
try
{
    var managed = Path.Combine(testRoot, "A Dance of Fire and Ice_Data", "Managed");
    Directory.CreateDirectory(managed);
    File.WriteAllBytes(Path.Combine(testRoot, "A Dance of Fire and Ice.exe"), []);
    File.WriteAllBytes(Path.Combine(managed, UmmSpecification.EntryPoint.AssemblyName), []);
    File.WriteAllBytes(Path.Combine(managed, "Assembly-CSharp.dll"), []);
    var layout = locator.Locate(testRoot)
        ?? throw new InvalidOperationException("A valid test game directory was rejected.");

    var settingsPath = Path.Combine(testRoot, "settings.json");
    var settings = new JsonAppLanguageStore(settingsPath);
    if (settings.LoadLanguage() != AppLanguage.System)
        throw new InvalidOperationException("Missing language settings did not default to System.");
    var localization = new LocalizationService(settings, () => CultureInfo.GetCultureInfo("en-US"));
    if (localization["Nav_Install"] != "Install")
        throw new InvalidOperationException("English Windows did not select English resources.");
    localization.SelectLanguage(AppLanguage.Korean);
    await settings.SaveGamePathAsync(layout.GameRoot);
    if (localization["Nav_Install"] != "설치" || settings.LoadLanguage() != AppLanguage.Korean)
        throw new InvalidOperationException("The selected language was not applied and persisted.");
    if (settings.LoadGamePath() != layout.GameRoot)
        throw new InvalidOperationException("Saving the language overwrote the selected game path.");
    localization.SelectLanguage(AppLanguage.System);
    var chinese = new LocalizationService(settings, () => CultureInfo.GetCultureInfo("zh-CN"));
    if (chinese["Nav_Install"] != "安装")
        throw new InvalidOperationException("Simplified Chinese system culture was not detected.");
    var unsupported = new LocalizationService(settings, () => CultureInfo.GetCultureInfo("fr-FR"));
    if (unsupported["Nav_Install"] != "Install")
        throw new InvalidOperationException("Unsupported system culture did not fall back to English.");

    await CheckCatalogAsync(testRoot);

    var validZip = Path.Combine(testRoot, "valid.zip");
    using (var archive = ZipFile.Open(validZip, ZipArchiveMode.Create))
    {
        WriteEntry(archive, "ExampleMod/Info.json",
            "{\"Id\":\"ExampleMod\",\"DisplayName\":\"Example Mod\",\"Version\":\"1.0.0\"}");
        WriteEntry(archive, "ExampleMod/ExampleMod.dll", "test");
    }
    var log = new BufferedOperationLog();
    var appData = new AppDataPaths(Path.Combine(testRoot, "app-data"));
    var modService = new FileModService(log, appData);
    var inspection = modService.Inspect(layout, validZip);
    if (inspection.Id != "ExampleMod" || inspection.DisplayName != "Example Mod")
        throw new InvalidOperationException("A valid mod archive was not inspected correctly.");

    var deletePath = Path.Combine(layout.ModsPath, "DeleteMe");
    Directory.CreateDirectory(deletePath);
    File.WriteAllText(Path.Combine(deletePath, "Info.json"), "{\"Id\":\"DeleteMe\"}");
    modService.Remove(layout, deletePath);
    if (Directory.Exists(deletePath) || Directory.EnumerateFileSystemEntries(appData.RemovedMods).Any())
        throw new InvalidOperationException("Permanent mod deletion created a recoverable backup.");

    var traversalZip = Path.Combine(testRoot, "traversal.zip");
    using (var archive = ZipFile.Open(traversalZip, ZipArchiveMode.Create))
    {
        WriteEntry(archive, "../Info.json", "{\"Id\":\"Unsafe\"}");
    }
    ExpectInvalidArchive(modService, layout, traversalZip, "path traversal");

    var ratioZip = Path.Combine(testRoot, "ratio.zip");
    using (var archive = ZipFile.Open(ratioZip, ZipArchiveMode.Create))
    {
        var entry = archive.CreateEntry("large-zero-file.bin", CompressionLevel.SmallestSize);
        using var stream = entry.Open();
        var zeros = new byte[1024 * 1024];
        for (var i = 0; i < 9; i++)
            stream.Write(zeros);
    }
    ExpectInvalidArchive(modService, layout, ratioZip, "unsafe compression ratio");

    Console.WriteLine("Windows core checks passed: localization, catalog, Discord parsing, layout validation, mod inspection, and ZIP guards.");
}
finally
{
    CatalogDownloadStorage.CleanupAll();
    if (Directory.Exists(testRoot))
        Directory.Delete(testRoot, recursive: true);
}

static void WriteEntry(ZipArchive archive, string name, string value)
{
    var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
    using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
    writer.Write(value);
}

static void ExpectInvalidArchive(FileModService modService, GameInstallation layout, string path, string scenario)
{
    try
    {
        _ = modService.Inspect(layout, path);
        throw new InvalidOperationException($"The {scenario} archive was accepted.");
    }
    catch (InvalidOperationException ex) when (!ex.Message.Contains("was accepted", StringComparison.Ordinal))
    {
    }
}

static async Task CheckCatalogAsync(string testRoot)
{
    var endpoint = new Uri("https://catalog.example/mods");
    var json = """
        [
          {"id":"old","name":"Timing Helper","cachedUsername":"Alice","description":"Editor tools","uploadedTimestamp":10,"parsedDownload":"https://example.com/old.zip"},
          {"id":"hidden","name":"Hidden","uploadedTimestamp":30,"hideFromSearch":true,"parsedDownload":"https://example.com/hidden.zip"},
          {"id":"new","name":"New Mod","uploadedTimestamp":20,"parsedDownload":"https://example.com/new.zip"}
        ]
        """;
    using var fetchClient = new HttpClient(new StubHandler(request => Response(request, Encoding.UTF8.GetBytes(json), "application/json")));
    var service = new ModCatalogClient(fetchClient, endpoint);
    var mods = await service.FetchModsAsync();
    if (mods.Select(mod => mod.Id).SequenceEqual(["new", "old"]) is false)
        throw new InvalidOperationException("Catalog filtering or newest-first sorting failed.");
    if (!mods[1].Matches("alice") || !mods[1].Matches("EDITOR"))
        throw new InvalidOperationException("Catalog author and description search failed.");

    var zip = new RemoteMod { Id = "zip", Name = "Zip", ParsedDownload = "https://example.com/mod.ZIP" };
    var github = new RemoteMod { Id = "github", Name = "GitHub", ParsedDownload = "https://github.com/a/b/releases/tag/v1" };
    var youtube = new RemoteMod { Id = "youtube", Name = "YouTube", ParsedDownload = "https://youtu.be/example" };
    var ambiguous = new RemoteMod { Id = "unknown", Name = "Unknown", ParsedDownload = "https://example.com/download?id=1" };
    if (zip.Action != RemoteModAction.DownloadAndInstall || github.Action != RemoteModAction.OpenWebsite
        || youtube.Action != RemoteModAction.OpenWebsite || ambiguous.Action != RemoteModAction.DownloadAndInspect)
        throw new InvalidOperationException("Catalog URL action classification failed.");

    var zipBytes = new byte[] { 0x50, 0x4b, 0x03, 0x04, 0x00 };
    using var downloadClient = new HttpClient(new StubHandler(request =>
    {
        var response = Response(request, zipBytes, "application/zip");
        response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://cdn.example/final.zip");
        return response;
    }));
    var downloader = new ModCatalogClient(downloadClient);
    var downloaded = await downloader.DownloadAsync(zip);
    if (!File.Exists(downloaded) || !ModCatalogClient.HasZipSignature(downloaded))
        throw new InvalidOperationException("A valid catalog ZIP was not retained.");
    CatalogDownloadStorage.RemoveOwnedFile(downloaded);

    using var htmlClient = new HttpClient(new StubHandler(request => Response(request, Encoding.UTF8.GetBytes("<html></html>"), "text/html")));
    await ExpectCatalogErrorAsync(new ModCatalogClient(htmlClient), ambiguous, ModCatalogError.NotZip);

    using var oversizedClient = new HttpClient(new StubHandler(request =>
    {
        var response = Response(request, zipBytes, "application/zip");
        response.Content.Headers.ContentLength = ModCatalogClient.MaximumDownloadBytes + 1;
        return response;
    }));
    await ExpectCatalogErrorAsync(new ModCatalogClient(oversizedClient), zip, ModCatalogError.FileTooLarge);

    using var actualSizeClient = new HttpClient(new StubHandler(request =>
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StreamContent(new MemoryStream(zipBytes))
        };
        response.Content.Headers.ContentLength = null;
        return response;
    }));
    await ExpectCatalogErrorAsync(new ModCatalogClient(actualSizeClient, maximumDownloadBytes: 4), zip, ModCatalogError.FileTooLarge);

    using var insecureRedirectClient = new HttpClient(new StubHandler(request =>
    {
        var response = Response(request, zipBytes, "application/zip");
        response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://cdn.example/mod.zip");
        return response;
    }));
    await ExpectCatalogErrorAsync(new ModCatalogClient(insecureRedirectClient), zip, ModCatalogError.InsecureUrl);

    using var httpErrorClient = new HttpClient(new StubHandler(request => new HttpResponseMessage(HttpStatusCode.NotFound)
    {
        RequestMessage = request
    }));
    await ExpectCatalogErrorAsync(new ModCatalogClient(httpErrorClient), zip, ModCatalogError.InvalidResponse);

    using var networkClient = new HttpClient(new StubHandler(_ => throw new HttpRequestException("offline")));
    try
    {
        await new ModCatalogClient(networkClient).DownloadAsync(zip);
        throw new InvalidOperationException("Catalog network failure was accepted.");
    }
    catch (HttpRequestException) { }

    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try
    {
        await downloader.DownloadAsync(zip, cancellationToken: cancellation.Token);
        throw new InvalidOperationException("Cancelled catalog download completed.");
    }
    catch (OperationCanceledException) { }

    var external = Path.Combine(testRoot, "external.zip");
    File.WriteAllBytes(external, zipBytes);
    CatalogDownloadStorage.RemoveOwnedFile(external);
    if (!File.Exists(external)) throw new InvalidOperationException("Catalog cleanup removed an external file.");

    var blocks = DiscordMessageParser.Parse("## Features\n- **Fast** loading\n> Safe\n1. First\n2. Second\n-# note\nName | Value\n--- | ---\nA | B\n---\n```cs\ncode\n```");
    if (blocks is not [DiscordHeading, DiscordBulletList, DiscordQuote, DiscordOrderedList,
        DiscordSubtext, DiscordTable, DiscordDivider, DiscordCodeBlock])
        throw new InvalidOperationException("Discord message blocks were not parsed correctly.");
    var inline = DiscordMessageParser.ParseInline("[Site](https://example.com) https://adofai.gg `safe` ~~old~~ __line__ ||spoiler||");
    if (!inline.Any(item => item.Link is not null) || !inline.Any(item => item.Code)
        || !inline.Any(item => item.Strikethrough) || !inline.Any(item => item.Underline)
        || !inline.Any(item => item.Spoiler))
        throw new InvalidOperationException("Discord inline formatting was not parsed correctly.");
}

static HttpResponseMessage Response(HttpRequestMessage request, byte[] body, string mediaType) => new(HttpStatusCode.OK)
{
    RequestMessage = request,
    Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue(mediaType) } }
};

static async Task ExpectCatalogErrorAsync(ModCatalogClient client, RemoteMod mod, ModCatalogError expected)
{
    try
    {
        await client.DownloadAsync(mod);
        throw new InvalidOperationException($"Catalog error {expected} was not raised.");
    }
    catch (ModCatalogException exception) when (exception.Error == expected) { }
}

sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(response(request));
    }
}
