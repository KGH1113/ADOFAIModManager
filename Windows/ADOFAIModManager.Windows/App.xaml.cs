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
        if (UserInstallService.HandleMaintenanceArguments())
        {
            Exit();
            return;
        }

        if (UserInstallService.InstallAndRelaunchIfNeeded())
        {
            Exit();
            return;
        }

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        _mainInstance = AppInstance.FindOrRegisterForKey("main");
        if (!_mainInstance.IsCurrent)
        {
            await _mainInstance.RedirectActivationToAsync(activation);
            Exit();
            return;
        }

        MainWindowInstance = new MainWindow();
        _mainInstance.Activated += (_, eventArgs) =>
            MainWindowInstance.DispatcherQueue.TryEnqueue(() => _ = HandleActivationAsync(eventArgs));

        UserInstallService.RegisterFileAssociations();
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
        await MainWindowInstance.HandleModFileAsync(file.Path);
    }
}
