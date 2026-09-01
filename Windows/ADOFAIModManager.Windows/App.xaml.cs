using ADOFAIModManager.Windows.Infrastructure.Installation;
using ADOFAIModManager.Windows.Services;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using WinUILaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace ADOFAIModManager.Windows;

public partial class App : Application
{
    private AppInstance? _mainInstance;
    public static MainWindow? MainWindowInstance { get; private set; }

    public App() => InitializeComponent();

    protected override async void OnLaunched(WinUILaunchActivatedEventArgs args)
    {
        if (UserInstallation.HandleMaintenanceArguments())
        {
            Exit();
            return;
        }

        if (UserInstallation.InstallAndRelaunchIfNeeded())
        {
            Exit();
            return;
        }

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        _mainInstance = AppInstance.FindOrRegisterForKey("main");
        if (!_mainInstance.IsCurrent)
        {
            WindowActivationService.BringProcessWindowToFront(_mainInstance.ProcessId);
            await _mainInstance.RedirectActivationToAsync(activation);
            Exit();
            return;
        }

        MainWindowInstance = new MainWindow(CompositionRoot.CreateMainViewModel());
        _mainInstance.Activated += (_, eventArgs) =>
            MainWindowInstance.DispatcherQueue.TryEnqueue(() => _ = HandleActivationAsync(eventArgs));

        FileAssociationRegistrar.Register(MainWindowInstance.ViewModel.Localization);
        MainWindowInstance.Activate();
        await MainWindowInstance.InitializeAsync();
        await HandleActivationAsync(activation);
    }

    private static async Task HandleActivationAsync(AppActivationArguments activation)
    {
        if (MainWindowInstance is null || activation.Kind != ExtendedActivationKind.File)
            return;
        if (activation.Data is not IFileActivatedEventArgs fileArgs)
            return;
        var file = fileArgs.Files.FirstOrDefault();
        if (file is null)
            return;
        WindowActivationService.BringWindowToFront(MainWindowInstance);
        await MainWindowInstance.HandleModFileAsync(file.Path);
        WindowActivationService.BringWindowToFront(MainWindowInstance);
    }
}
