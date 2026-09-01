using ADOFAIModManager.Windows.Infrastructure.Process;
using ADOFAIModManager.Windows.Infrastructure.Localization;
using ADOFAIModManager.Windows.Infrastructure.Logs;
using ADOFAIModManager.Windows.Infrastructure.Steam;
using ADOFAIModManager.Windows.Infrastructure.Shell;
using ADOFAIModManager.Windows.Infrastructure.Catalog;
using ADOFAIModManager.Windows.Services;
using ADOFAIModManager.Windows.ViewModels;
using NativeUmm.Infrastructure.Installation;
using NativeUmm.Infrastructure.Logging;
using NativeUmm.Infrastructure.Mods;
using NativeUmm.Infrastructure.Storage;

namespace ADOFAIModManager.Windows;

internal static class CompositionRoot
{
    public static MainViewModel CreateMainViewModel()
    {
        var log = new BufferedOperationLog();
        var appData = AppDataPaths.ForWindows();
        var settingsPath = Path.Combine(Path.GetDirectoryName(appData.Cache)!, "settings.json");
        var settings = new JsonAppLanguageStore(settingsPath);
        var localization = new LocalizationService(settings);
        var gameService = new GameService(
            new WindowsGameLocator(),
            new WindowsGameProcessProbe(),
            new DnlibUmmInstaller(log, appData),
            new FileModService(log, appData),
            log,
            appData);
        var workspace = new WindowsWorkspaceShell();
        var catalogClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        });
        return new MainViewModel(
            gameService,
            localization,
            settings,
            workspace,
            new WindowsLogReader(localization),
            new ModCatalogClient(catalogClient));
    }
}
