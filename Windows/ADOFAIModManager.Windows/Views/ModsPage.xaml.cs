using ADOFAIModManager.Windows.Models;
using ADOFAIModManager.Windows.Services;
using ADOFAIModManager.Windows.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ADOFAIModManager.Windows.Views;

public sealed partial class ModsPage : Page
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    internal ModsPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void AddMod_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindowInstance is null) return;
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".zip");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
            await ImportPathAsync(file.Path);
    }

    internal async Task ImportPathAsync(string path)
    {
        if (!Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return;
        var preview = await ViewModel.InspectModAsync(path);
        if (preview is null)
            return;

        var version = string.IsNullOrWhiteSpace(preview.Version) ? "버전 정보 없음" : $"버전 {preview.Version}";
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = preview.AlreadyInstalled ? $"‘{preview.Name}’ 모드를 교체하시겠습니까?" : $"‘{preview.Name}’ 모드를 추가하시겠습니까?",
            Content = $"{version}\n{preview.Id}",
            PrimaryButtonText = preview.AlreadyInstalled ? "교체" : "모드 추가",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.InstallModAsync(preview);
    }

    private async void ModToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle || toggle.DataContext is not ModViewItem mod)
            return;
        await ViewModel.SetModEnabledAsync(mod, toggle.IsChecked == true);
    }

    private async void DeleteMod_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ModViewItem mod)
            return;
        await ViewModel.PermanentlyRemoveModAsync(mod);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshAsync();
    private void OpenFolder_Click(object sender, RoutedEventArgs e) => ShellService.OpenFolder(ViewModel.ModsDirectory);

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "UMM 모드 확인";
        DropOverlay.Visibility = Visibility.Visible;
    }

    private void Page_DragLeave(object sender, DragEventArgs e) =>
        DropOverlay.Visibility = Visibility.Collapsed;

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;
        var items = await e.DataView.GetStorageItemsAsync();
        var file = items.OfType<StorageFile>().FirstOrDefault(item =>
            item.FileType.Equals(".zip", StringComparison.OrdinalIgnoreCase));
        if (file is not null)
            await ImportPathAsync(file.Path);
    }
}
