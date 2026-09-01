using ADOFAIModManager.Windows.Application.Localization;
using ADOFAIModManager.Windows.Models;
using ADOFAIModManager.Windows.ViewModels;

namespace ADOFAIModManager.Windows.Features.Installation;

internal sealed class InstallationViewModel : ObservableObject
{
    private readonly AppSession session;
    private string gameLocationText = "";
    private string ummStatusText = "";
    private string primaryInstallText = "";
    private string installWarning = "";
    private bool foundThroughSteam;
    private bool managerInstalled;
    private string? managerVersion;

    public InstallationViewModel(AppSession session)
    {
        this.session = session;
        Localization = session.Localization;
        RefreshLocalizedStatus();
        Localization.LanguageChanged += (_, _) => RefreshLocalizedStatus();
        session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AppSession.IsBusy))
            {
                RaisePropertyChanged(nameof(CanInstall));
                RaisePropertyChanged(nameof(CanUseGameActions));
            }
        };
    }

    internal Func<Task>? RefreshRequested { get; set; }
    public ILocalizationService Localization { get; }
    public string GameLocationText { get => gameLocationText; private set => SetProperty(ref gameLocationText, value); }
    public string UmmStatusText { get => ummStatusText; private set => SetProperty(ref ummStatusText, value); }
    public string PrimaryInstallText { get => primaryInstallText; private set => SetProperty(ref primaryInstallText, value); }
    public string InstallWarning
    {
        get => installWarning;
        private set
        {
            if (!SetProperty(ref installWarning, value)) return;
            RaisePropertyChanged(nameof(HasInstallWarning));
        }
    }
    public bool HasInstallWarning => !string.IsNullOrWhiteSpace(InstallWarning);
    public bool HasGame => session.Installation is not null;
    public bool CanInstall => HasGame && !session.IsBusy;
    public bool CanUseGameActions => HasGame && !session.IsBusy;
    public string? GameDirectory => session.Installation?.GameRoot;

    public Task InitializeAsync() => session.RunAsync("Activity_FindGame", async () =>
    {
        session.Installation = await Task.Run(() => session.GameService.Detect(session.Settings.LoadGamePath()));
        if (RefreshRequested is not null) await RefreshRequested();
    });

    public Task SelectGameAsync(string path) => session.RunAsync("Activity_CheckGame", async () =>
    {
        var detected = await Task.Run(() => session.GameService.Detect(path));
        if (detected is null)
            throw new InvalidOperationException(Localization["Error_SelectedGameNotFound"]);
        var selectedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var detectedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(detected.GameRoot));
        if (!detectedPath.Equals(selectedPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(Localization["Error_SelectedGameNotFound"]);
        session.Installation = detected;
        await session.Settings.SaveGamePathAsync(detected.GameRoot);
        if (RefreshRequested is not null) await RefreshRequested();
    });

    public Task RefreshAsync() => session.RunAsync("Activity_CheckStatus", async () =>
    {
        if (RefreshRequested is not null) await RefreshRequested();
    });

    public async Task InstallAsync(bool repair = false)
    {
        await session.RunGameActionAsync(repair ? "Activity_Repair" : "Activity_Install",
            layout => session.GameService.InstallAsync(layout, repair));
        if (!session.HasError && RefreshRequested is not null) await RefreshRequested();
    }

    public async Task RemoveUmmAsync()
    {
        await session.RunGameActionAsync("Activity_RemoveUmm", layout =>
        {
            session.GameService.RemoveUmm(layout);
            return Task.CompletedTask;
        });
        if (!session.HasError && RefreshRequested is not null) await RefreshRequested();
    }

    public async Task RestoreOriginalAsync()
    {
        await session.RunGameActionAsync("Activity_Restore", layout =>
        {
            session.GameService.RestoreOriginal(layout);
            return Task.CompletedTask;
        });
        if (!session.HasError && RefreshRequested is not null) await RefreshRequested();
    }

    internal async Task RefreshCoreAsync()
    {
        session.Installation ??= await Task.Run(() => session.GameService.Detect(null));
        if (session.Installation is null)
        {
            foundThroughSteam = false;
            managerInstalled = false;
            managerVersion = null;
            InstallWarning = "";
            RefreshLocalizedStatus();
            RaiseGameState();
            return;
        }

        var layout = session.Installation;
        var status = await Task.Run(() => session.GameService.ReadStatus(layout));
        foundThroughSteam = session.GameService.Detect(null)?.GameRoot.Equals(
            layout.GameRoot, StringComparison.OrdinalIgnoreCase) == true;
        managerInstalled = status.ManagerInstalled;
        managerVersion = status.ManagerVersion;
        InstallWarning = status.Warning ?? "";
        RefreshLocalizedStatus();
        RaiseGameState();
    }

    private void RefreshLocalizedStatus()
    {
        if (session.Installation is null)
        {
            GameLocationText = Localization["Install_StatusNotFound"];
            UmmStatusText = Localization["Install_StatusChooseGame"];
        }
        else
        {
            GameLocationText = Localization[foundThroughSteam ? "Install_StatusSteam" : "Install_StatusManual"];
            UmmStatusText = managerInstalled
                ? Localization.Format("Install_StatusInstalled", managerVersion ?? Localization["Common_Unknown"])
                : Localization["Install_StatusRequired"];
        }
        PrimaryInstallText = Localization[managerInstalled ? "Install_ActionReinstall" : "Install_ActionInstall"];
    }

    private void RaiseGameState()
    {
        RaisePropertyChanged(nameof(HasGame));
        RaisePropertyChanged(nameof(CanInstall));
        RaisePropertyChanged(nameof(CanUseGameActions));
        RaisePropertyChanged(nameof(GameDirectory));
    }
}
