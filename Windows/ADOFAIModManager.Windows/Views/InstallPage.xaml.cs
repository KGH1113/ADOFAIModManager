using ADOFAIModManager.Windows.Features.Installation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ADOFAIModManager.Windows.Views;

public sealed partial class InstallPage : Page
{
    private InstallationViewModel ViewModel => (InstallationViewModel)DataContext;

    internal InstallPage(InstallationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void ChooseGame_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        if (App.MainWindowInstance is null) return;
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            await ViewModel.SelectGameAsync(folder.Path);
    }

    private async void Install_Click(object sender, RoutedEventArgs e) => await ViewModel.InstallAsync();
    private async void Repair_Click(object sender, RoutedEventArgs e) => await ViewModel.InstallAsync(repair: true);
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshAsync();

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmAsync("Install_ConfirmRemoveTitle", "Install_ConfirmRemoveMessage"))
            return;
        await ViewModel.RemoveUmmAsync();
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmAsync("Install_ConfirmRestoreTitle", "Install_ConfirmRestoreMessage"))
            return;
        await ViewModel.RestoreOriginalAsync();
    }

    private async Task<bool> ConfirmAsync(string titleKey, string messageKey)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = ViewModel.Localization[titleKey],
            Content = ViewModel.Localization[messageKey],
            PrimaryButtonText = ViewModel.Localization["Common_Continue"],
            CloseButtonText = ViewModel.Localization["Common_Cancel"],
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
