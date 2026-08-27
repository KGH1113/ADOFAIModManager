using System.Text.Json;
using ADOFAIModManager.Windows.Models;
using ADOFAIModManager.Windows.ViewModels;

namespace ADOFAIModManager.Windows.Features.Installation;

internal sealed class InstallationViewModel : ObservableObject
{
    private readonly AppSession session;
    private string gameLocationText = "찾지 못함";
    private string ummStatusText = "설치 필요";
    private string primaryInstallText = "UMM 설치";
    private string installWarning = "";

    public InstallationViewModel(AppSession session)
    {
        this.session = session;
        session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AppSession.IsBusy))
            {
                RaisePropertyChanged(nameof(CanInstall));
                RaisePropertyChanged(nameof(CanUseGameActions));
            }
        };
    }

    internal Func<Task>? RefreshRequested { get; set; }
    public string GameLocationText { get => gameLocationText; private set => SetProperty(ref gameLocationText, value); }
    public string UmmStatusText { get => ummStatusText; private set => SetProperty(ref ummStatusText, value); }
    public string PrimaryInstallText { get => primaryInstallText; private set => SetProperty(ref primaryInstallText, value); }
    public string InstallWarning
    {
        get => installWarning;
        private set
        {
            if (!SetProperty(ref installWarning, value)) return;
            RaisePropertyChanged(nameof(HasInstallWarning));
        }
    }
    public bool HasInstallWarning => !string.IsNullOrWhiteSpace(InstallWarning);
    public bool HasGame => session.Installation is not null;
    public bool CanInstall => HasGame && !session.IsBusy;
    public bool CanUseGameActions => HasGame && !session.IsBusy;
    public string? GameDirectory => session.Installation?.GameRoot;

    public Task InitializeAsync() => session.RunAsync("얼불춤 찾는 중…", async () =>
    {
        var saved = await ReadSavedGamePathAsync();
        session.Installation = await Task.Run(() => session.GameService.Detect(saved));
        if (RefreshRequested is not null) await RefreshRequested();
    });

    public Task SelectGameAsync(string path) => session.RunAsync("얼불춤 확인 중…", async () =>
    {
        var detected = await Task.Run(() => session.GameService.Detect(path));
        if (detected is null)
            throw new InvalidOperationException("선택한 폴더에서 얼불춤을 찾을 수 없습니다.");
        var selectedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var detectedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(detected.GameRoot));
        if (!detectedPath.Equals(selectedPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("선택한 폴더에서 얼불춤을 찾을 수 없습니다.");
        session.Installation = detected;
        await SaveGamePathAsync(detected.GameRoot);
        if (RefreshRequested is not null) await RefreshRequested();
    });

    public Task RefreshAsync() => session.RunAsync("상태 확인 중…", async () =>
    {
        if (RefreshRequested is not null) await RefreshRequested();
    });

    public async Task InstallAsync(bool repair = false)
    {
        await session.RunGameActionAsync(repair ? "설치 문제 해결 중…" : "UMM 설치 중…",
            layout => session.GameService.InstallAsync(layout, repair));
        if (!session.HasError && RefreshRequested is not null) await RefreshRequested();
    }

    public async Task RemoveUmmAsync()
    {
        await session.RunGameActionAsync("UMM 제거 중…", layout =>
        {
            session.GameService.RemoveUmm(layout);
            return Task.CompletedTask;
        });
        if (!session.HasError && RefreshRequested is not null) await RefreshRequested();
    }

    public async Task RestoreOriginalAsync()
    {
        await session.RunGameActionAsync("되돌리는 중…", layout =>
        {
            session.GameService.RestoreOriginal(layout);
            return Task.CompletedTask;
        });
        if (!session.HasError && RefreshRequested is not null) await RefreshRequested();
    }

    internal async Task RefreshCoreAsync()
    {
        session.Installation ??= await Task.Run(() => session.GameService.Detect(null));
        if (session.Installation is null)
        {
            GameLocationText = "찾지 못함";
            UmmStatusText = "얼불춤을 먼저 선택해 주세요";
            PrimaryInstallText = "UMM 설치";
            InstallWarning = "";
            RaiseGameState();
            return;
        }

        var layout = session.Installation;
        var status = await Task.Run(() => session.GameService.ReadStatus(layout));
        GameLocationText = session.GameService.Detect(null)?.GameRoot.Equals(layout.GameRoot, StringComparison.OrdinalIgnoreCase) == true
            ? "Steam에서 찾음" : "직접 선택함";
        UmmStatusText = status.ManagerInstalled
            ? $"설치됨 · 버전 {status.ManagerVersion ?? "알 수 없음"}" : "설치 필요";
        PrimaryInstallText = status.ManagerInstalled ? "다시 설치" : "UMM 설치";
        InstallWarning = status.Warning ?? "";
        RaiseGameState();
    }

    private void RaiseGameState()
    {
        RaisePropertyChanged(nameof(HasGame));
        RaisePropertyChanged(nameof(CanInstall));
        RaisePropertyChanged(nameof(CanUseGameActions));
        RaisePropertyChanged(nameof(GameDirectory));
    }

    private async Task<string?> ReadSavedGamePathAsync()
    {
        try
        {
            if (!File.Exists(session.SettingsPath)) return null;
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(session.SettingsPath));
            return document.RootElement.TryGetProperty("gamePath", out var value) ? value.GetString() : null;
        }
        catch { return null; }
    }

    private Task SaveGamePathAsync(string path) =>
        File.WriteAllTextAsync(session.SettingsPath, JsonSerializer.Serialize(new { gamePath = path }));
}
