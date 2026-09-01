using ADOFAIModManager.Windows.Features.Installation;
using ADOFAIModManager.Windows.Features.Logs;
using ADOFAIModManager.Windows.Features.Mods;
using ADOFAIModManager.Windows.Features.Catalog;
using ADOFAIModManager.Windows.Models;
using ADOFAIModManager.Windows.Services;
using Microsoft.UI.Xaml;
using NativeUmm.Application.Abstractions;
using ADOFAIModManager.Windows.Application.Abstractions;
using ADOFAIModManager.Windows.Application.Localization;
using ADOFAIModManager.Windows.Application.Catalog;

namespace ADOFAIModManager.Windows.ViewModels;

internal sealed class MainViewModel : ObservableObject
{
    private readonly AppSession session;

    public MainViewModel(
        GameService gameService,
        ILocalizationService localization,
        IAppLanguageStore settings,
        IWorkspaceShell workspace,
        IWindowsLogReader logReader,
        IModCatalogService catalogService)
    {
        session = new AppSession(gameService, localization, settings);
        Installation = new InstallationViewModel(session);
        Mods = new ModsViewModel(session, workspace);
        Catalog = new CatalogViewModel(catalogService, workspace, localization);
        Logs = new LogsViewModel(session, workspace, logReader);
        Installation.RefreshRequested = RefreshAllCoreAsync;
        Mods.RefreshRequested = RefreshAllCoreAsync;
        session.PropertyChanged += (_, args) => RaisePropertyChanged(args.PropertyName);
    }

    public InstallationViewModel Installation { get; }
    public ModsViewModel Mods { get; }
    public CatalogViewModel Catalog { get; }
    public LogsViewModel Logs { get; }
    public ILocalizationService Localization => session.Localization;
    public bool IsBusy => session.IsBusy;
    public Visibility BusyVisibility => session.BusyVisibility;
    public string Activity => session.Activity;
    public string LastError => session.LastError;
    public bool HasError => session.HasError;

    public Task InitializeAsync() => Installation.InitializeAsync();
    public Task RefreshAsync() => session.RunAsync("Activity_CheckStatus", RefreshAllCoreAsync);
    public void DismissError() => session.DismissError();

    private async Task RefreshAllCoreAsync()
    {
        await Installation.RefreshCoreAsync();
        await Mods.RefreshCoreAsync();
        await Logs.RefreshLogsAsync();
    }
}
