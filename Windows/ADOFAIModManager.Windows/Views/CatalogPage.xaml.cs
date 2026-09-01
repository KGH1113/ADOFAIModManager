using System.ComponentModel;
using ADOFAIModManager.Windows.Application.Catalog;
using ADOFAIModManager.Windows.Features.Catalog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ADOFAIModManager.Windows.Views;

public sealed partial class CatalogPage : Page
{
    private CatalogViewModel ViewModel => (CatalogViewModel)DataContext;
    private readonly ModImportWorkflow importWorkflow;
    private readonly Action showMods;

    internal CatalogPage(CatalogViewModel viewModel, ModImportWorkflow importWorkflow, Action showMods)
    {
        this.importWorkflow = importWorkflow;
        this.showMods = showMods;
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdateDetail();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e) => await ViewModel.LoadIfNeededAsync();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshAsync();
    private void CancelDownload_Click(object sender, RoutedEventArgs e) => ViewModel.CancelDownload();
    private void DownloadError_CloseButtonClick(InfoBar sender, object args) => ViewModel.DismissDownloadError();

    private async void PrimaryAction_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedMod is not { } mod) return;
        if (mod.Action == RemoteModAction.OpenWebsite)
        {
            if (!ViewModel.OpenWebsite(mod)) ViewModel.ReportWebsiteError();
            return;
        }

        try
        {
            var path = await ViewModel.DownloadAsync(mod);
            if (path is null) return;
            if (await importWorkflow.ImportAsync(path, XamlRoot, deleteWhenFinished: true))
                showMods();
        }
        catch (ModCatalogException exception) when (exception.Error == ModCatalogError.NotZip)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = ViewModel.Localization["Catalog_NotInstallableTitle"],
                Content = ViewModel.Localization["Catalog_NotInstallableMessage"],
                PrimaryButtonText = ViewModel.Localization["Catalog_OpenWebsite"],
                CloseButtonText = ViewModel.Localization["Common_Cancel"],
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !ViewModel.OpenWebsite(mod))
                ViewModel.ReportWebsiteError();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CatalogViewModel.SelectedMod)) UpdateDetail();
    }

    private void UpdateDetail()
    {
        var mod = ViewModel.SelectedMod;
        DiscordMessageRenderer.Render(DescriptionPanel, mod?.Description ?? "");
        if (mod?.ImageUri is { } imageUri)
        {
            PreviewImage.Source = new BitmapImage(imageUri);
            PreviewImage.Visibility = Visibility.Visible;
        }
        else
        {
            PreviewImage.Source = null;
            PreviewImage.Visibility = Visibility.Collapsed;
        }
    }
}
