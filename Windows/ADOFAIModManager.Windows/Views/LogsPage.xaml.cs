using ADOFAIModManager.Windows.Features.Logs;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ADOFAIModManager.Windows.Views;

public sealed partial class LogsPage : Page
{
    private LogsViewModel ViewModel => (LogsViewModel)DataContext;

    internal LogsPage(LogsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshLogsAsync();
    private void OpenGameLog_Click(object sender, RoutedEventArgs e) => ViewModel.OpenGameLog();
    private void OpenUmmLog_Click(object sender, RoutedEventArgs e) => ViewModel.OpenUmmLog();
}
