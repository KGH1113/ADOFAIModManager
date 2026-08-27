using ADOFAIModManager.Windows.Infrastructure.Process;
using ADOFAIModManager.Windows.Infrastructure.Logs;
using ADOFAIModManager.Windows.Infrastructure.Steam;
using ADOFAIModManager.Windows.Infrastructure.Shell;
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
        var gameService = new GameService(
            new WindowsGameLocator(),
            new WindowsGameProcessProbe(),
            new DnlibUmmInstaller(log, appData),
            new FileModService(log, appData),
            log,
            appData);
        return new MainViewModel(
            gameService,
            appData,
            new WindowsWorkspaceShell(),
            new WindowsLogReader());
    }
}
