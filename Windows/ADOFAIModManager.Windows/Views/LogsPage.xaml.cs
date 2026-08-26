using ADOFAIModManager.Windows.Services;
using ADOFAIModManager.Windows.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ADOFAIModManager.Windows.Views;

public sealed partial class LogsPage : Page
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    internal LogsPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshLogsAsync();
    private void OpenGameLog_Click(object sender, RoutedEventArgs e) => ShellService.OpenFileLocation(ViewModel.GameLogPath);
    private void OpenUmmLog_Click(object sender, RoutedEventArgs e) => ShellService.OpenFileLocation(ViewModel.UmmLogPath);
}
