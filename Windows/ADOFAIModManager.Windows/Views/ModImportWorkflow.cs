using ADOFAIModManager.Windows.Features.Mods;
using ADOFAIModManager.Windows.Infrastructure.Catalog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ADOFAIModManager.Windows.Views;

internal sealed class ModImportWorkflow(ModsViewModel viewModel)
{
    public async Task<bool> ImportAsync(string path, XamlRoot xamlRoot, bool deleteWhenFinished)
    {
        try
        {
            if (!Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                return false;
            var preview = await viewModel.InspectModAsync(path);
            if (preview is null) return false;

            var version = string.IsNullOrWhiteSpace(preview.Version)
                ? viewModel.Localization["Mods_VersionUnknown"]
                : viewModel.Localization.Format("Mods_Version", preview.Version);
            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = viewModel.Localization.Format(
                    preview.AlreadyInstalled ? "Mods_ConfirmReplace" : "Mods_ConfirmAdd", preview.Name),
                Content = $"{version}\n{preview.Id}",
                PrimaryButtonText = viewModel.Localization[preview.AlreadyInstalled ? "Mods_Replace" : "Mods_Add"],
                CloseButtonText = viewModel.Localization["Common_Cancel"],
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;
            return await viewModel.InstallModAsync(preview);
        }
        finally
        {
            if (deleteWhenFinished) CatalogDownloadStorage.RemoveOwnedFile(path);
        }
    }
}
