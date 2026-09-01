using System.Collections.ObjectModel;
using System.Globalization;
using ADOFAIModManager.Windows.Application.Abstractions;
using ADOFAIModManager.Windows.Application.Catalog;
using ADOFAIModManager.Windows.Application.Localization;
using ADOFAIModManager.Windows.Models;
using Microsoft.UI.Xaml;

namespace ADOFAIModManager.Windows.Features.Catalog;

internal sealed class CatalogViewModel : ObservableObject
{
    private readonly IModCatalogService service;
    private readonly IWorkspaceShell workspace;
    private readonly List<RemoteMod> allMods = [];
    private string searchText = "";
    private RemoteMod? selectedMod;
    private bool isLoading;
    private string? loadError;
    private string? downloadError;
    private string? loadErrorKey;
    private string? downloadErrorKey;
    private string? downloadingId;
    private double? downloadProgress;
    private CancellationTokenSource? downloadCancellation;
    private bool didLoad;

    public CatalogViewModel(IModCatalogService service, IWorkspaceShell workspace, ILocalizationService localization)
    {
        this.service = service;
        this.workspace = workspace;
        Localization = localization;
        localization.LanguageChanged += (_, _) =>
        {
            if (loadErrorKey is not null) LoadError = localization[loadErrorKey];
            if (downloadErrorKey is not null) DownloadError = localization[downloadErrorKey];
            RaisePropertyChanged(nameof(SelectedUploadedDate));
            RaisePropertyChanged(nameof(DownloadProgressText));
        };
    }

    public ILocalizationService Localization { get; }
    public ObservableCollection<RemoteMod> VisibleMods { get; } = [];
    public string SearchText
    {
        get => searchText;
        set { if (SetProperty(ref searchText, value)) ApplyFilter(); }
    }
    public RemoteMod? SelectedMod
    {
        get => selectedMod;
        set
        {
            if (!SetProperty(ref selectedMod, value)) return;
            RaiseDetailProperties();
        }
    }
    public bool IsLoading { get => isLoading; private set { if (SetProperty(ref isLoading, value)) RaiseStateProperties(); } }
    public string? LoadError { get => loadError; private set { if (SetProperty(ref loadError, value)) RaiseStateProperties(); } }
    public string? DownloadError
    {
        get => downloadError;
        private set
        {
            if (!SetProperty(ref downloadError, value)) return;
            RaisePropertyChanged(nameof(HasDownloadError));
        }
    }
    public bool HasDownloadError => !string.IsNullOrWhiteSpace(DownloadError);
    public string? DownloadingId { get => downloadingId; private set { if (SetProperty(ref downloadingId, value)) RaiseDetailProperties(); } }
    public double? DownloadProgress { get => downloadProgress; private set { if (SetProperty(ref downloadProgress, value)) RaisePropertyChanged(nameof(DownloadProgressText)); } }
    public string DownloadProgressText => DownloadProgress is { } value
        ? Localization.Format("Catalog_DownloadPercent", Math.Clamp(value, 0, 1).ToString("P0", CultureInfo.CurrentCulture))
        : Localization["Catalog_Downloading"];
    public string SelectedUploadedDate => SelectedMod?.UploadedTimestamp > 0
        ? DateTimeOffset.FromUnixTimeMilliseconds(SelectedMod.UploadedTimestamp).ToString("D", CultureForLanguage())
        : "";
    public Visibility LoadingVisibility => IsLoading && allMods.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LoadErrorVisibility => LoadError is not null && allMods.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyVisibility => !IsLoading && LoadError is null && allMods.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ResultsVisibility => allMods.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SearchEmptyVisibility => allMods.Count > 0 && VisibleMods.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DetailVisibility => SelectedMod is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility NoSelectionVisibility => SelectedMod is null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DownloadButtonVisibility => SelectedMod is not null && DownloadingId is null
        && SelectedMod.Action != RemoteModAction.OpenWebsite ? Visibility.Visible : Visibility.Collapsed;
    public Visibility WebsiteButtonVisibility => SelectedMod is not null && DownloadingId is null
        && SelectedMod.Action == RemoteModAction.OpenWebsite ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DownloadingVisibility => SelectedMod is not null && DownloadingId is not null
        ? Visibility.Visible : Visibility.Collapsed;

    public Task LoadIfNeededAsync() => didLoad ? Task.CompletedTask : RefreshAsync();

    public async Task RefreshAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        loadErrorKey = null;
        LoadError = null;
        downloadErrorKey = null;
        DownloadError = null;
        try
        {
            var selectedId = SelectedMod?.Id;
            var result = await service.FetchModsAsync();
            allMods.Clear();
            allMods.AddRange(result);
            didLoad = true;
            ApplyFilter(selectedId);
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (allMods.Count == 0)
            {
                loadErrorKey = "Catalog_LoadError";
                LoadError = Localization[loadErrorKey];
            }
            else SetDownloadError("Catalog_RefreshError");
        }
        finally
        {
            IsLoading = false;
            RaiseStateProperties();
        }
    }

    public async Task<string?> DownloadAsync(RemoteMod mod)
    {
        if (DownloadingId is not null) return null;
        downloadErrorKey = null;
        DownloadError = null;
        DownloadProgress = null;
        DownloadingId = mod.Id;
        downloadCancellation = new CancellationTokenSource();
        try
        {
            var progress = new Progress<double?>(value => DownloadProgress = value);
            return await service.DownloadAsync(mod, progress, downloadCancellation.Token);
        }
        catch (OperationCanceledException) { return null; }
        catch (ModCatalogException exception) when (exception.Error == ModCatalogError.NotZip)
        {
            throw;
        }
        catch (ModCatalogException exception) when (exception.Error == ModCatalogError.FileTooLarge)
        {
            SetDownloadError("Catalog_FileTooLarge");
            return null;
        }
        catch
        {
            SetDownloadError("Catalog_DownloadError");
            return null;
        }
        finally
        {
            downloadCancellation.Dispose();
            downloadCancellation = null;
            DownloadingId = null;
            DownloadProgress = null;
        }
    }

    public void CancelDownload() => downloadCancellation?.Cancel();
    public bool OpenWebsite(RemoteMod mod) => mod.PreferredDownloadUri is { } uri && workspace.OpenWebsite(uri);
    public void ReportWebsiteError() => SetDownloadError("Catalog_WebsiteError");
    public void DismissDownloadError()
    {
        downloadErrorKey = null;
        DownloadError = null;
    }

    private void ApplyFilter(string? preferredSelection = null)
    {
        preferredSelection ??= SelectedMod?.Id;
        VisibleMods.Clear();
        foreach (var mod in allMods.Where(mod => mod.Matches(SearchText))) VisibleMods.Add(mod);
        SelectedMod = VisibleMods.FirstOrDefault(mod => mod.Id == preferredSelection) ?? VisibleMods.FirstOrDefault();
        RaiseStateProperties();
    }

    private void RaiseStateProperties()
    {
        RaisePropertyChanged(nameof(LoadingVisibility));
        RaisePropertyChanged(nameof(LoadErrorVisibility));
        RaisePropertyChanged(nameof(EmptyVisibility));
        RaisePropertyChanged(nameof(ResultsVisibility));
        RaisePropertyChanged(nameof(SearchEmptyVisibility));
    }

    private void RaiseDetailProperties()
    {
        RaisePropertyChanged(nameof(DetailVisibility));
        RaisePropertyChanged(nameof(NoSelectionVisibility));
        RaisePropertyChanged(nameof(DownloadButtonVisibility));
        RaisePropertyChanged(nameof(WebsiteButtonVisibility));
        RaisePropertyChanged(nameof(DownloadingVisibility));
        RaisePropertyChanged(nameof(SelectedUploadedDate));
    }

    private void SetDownloadError(string key)
    {
        downloadErrorKey = key;
        DownloadError = Localization[key];
    }

    private CultureInfo CultureForLanguage() => Localization.EffectiveLanguage switch
    {
        AppLanguage.Korean => CultureInfo.GetCultureInfo("ko-KR"),
        AppLanguage.SimplifiedChinese => CultureInfo.GetCultureInfo("zh-CN"),
        _ => CultureInfo.GetCultureInfo("en-US")
    };
}
