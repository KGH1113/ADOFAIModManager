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

    Console.WriteLine("Windows core checks passed: localization, UMM startup points, layout validation, mod inspection, and ZIP guards.");
}
finally
{
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
