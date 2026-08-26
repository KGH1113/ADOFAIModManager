using ADOFAIModManager.Windows.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ADOFAIModManager.Windows.Views;

public sealed partial class InstallPage : Page
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    internal InstallPage(MainViewModel viewModel)
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
        if (!await ConfirmAsync("UMM을 제거하시겠습니까?", "게임 시작 파일에서 UMM 연결을 제거하고 UMM 파일을 복구 가능한 위치로 옮깁니다."))
            return;
        await ViewModel.RemoveUmmAsync();
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmAsync("설치 전 상태로 되돌리시겠습니까?", "보관된 원본 게임 파일을 복원합니다."))
            return;
        await ViewModel.RestoreOriginalAsync();
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = "계속",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
