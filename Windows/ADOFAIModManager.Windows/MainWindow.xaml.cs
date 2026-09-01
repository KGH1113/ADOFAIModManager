using ADOFAIModManager.Windows.ViewModels;
using ADOFAIModManager.Windows.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace ADOFAIModManager.Windows;

public sealed partial class MainWindow : Window
{
    internal MainViewModel ViewModel { get; }
    private readonly InstallPage _installPage;
    private readonly ModsPage _modsPage;
    private readonly LogsPage _logsPage;
    private readonly SettingsPage _settingsPage;

    internal MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Root.DataContext = ViewModel;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);

        _installPage = new InstallPage(ViewModel.Installation);
        _modsPage = new ModsPage(ViewModel.Mods);
        _logsPage = new LogsPage(ViewModel.Logs);
        _settingsPage = new SettingsPage(ViewModel.Localization);

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new global::Windows.Graphics.SizeInt32(1040, 720));
        var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(icon))
            appWindow.SetIcon(icon);

        Navigation.SelectedItem = Navigation.MenuItems[0];
        ContentFrame.Content = _installPage;
    }

    public async Task InitializeAsync() => await ViewModel.InitializeAsync();

    public async Task HandleModFileAsync(string path)
    {
        Navigation.SelectedItem = Navigation.MenuItems[1];
        ContentFrame.Content = _modsPage;
        await _modsPage.ImportPathAsync(path);
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag)
            return;
        ContentFrame.Content = tag switch
        {
            "mods" => _modsPage,
            "logs" => _logsPage,
            _ => _installPage
        };
        if (tag == "logs")
            _ = ViewModel.Logs.RefreshLogsAsync();
    }

    private void ErrorInfoBar_CloseButtonClick(InfoBar sender, object args) => ViewModel.DismissError();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        Navigation.SelectedItem = null;
        ContentFrame.Content = _settingsPage;
    }
}
