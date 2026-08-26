using System.Collections.ObjectModel;
using System.Text.Json;
using ADOFAIModManager.Windows.Models;
using ADOFAIModManager.Windows.Services;
using Microsoft.UI.Xaml;
using NativeUmm;

namespace ADOFAIModManager.Windows.ViewModels;

internal sealed class MainViewModel : ObservableObject
{
    private readonly GameService _gameService = new();
    private GameLayout? _layout;
    private bool _isBusy;
    private string _activity = "";
    private string _gameLocationText = "찾지 못함";
    private string _ummStatusText = "설치 필요";
    private string _primaryInstallText = "UMM 설치";
    private string _installWarning = "";
    private string _lastError = "";
    private string _gameLogText = "";
    private string _ummLogText = "";

    public ObservableCollection<ModViewItem> Mods { get; } = [];
    public ObservableCollection<LogEntry> OperationLogs { get; } = [];

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            RaisePropertyChanged(nameof(CanUseGameActions));
            RaisePropertyChanged(nameof(CanInstall));
            RaisePropertyChanged(nameof(BusyVisibility));
        }
    }

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public string Activity
    {
        get => _activity;
        private set => SetProperty(ref _activity, value);
    }

    public string GameLocationText
    {
        get => _gameLocationText;
        private set => SetProperty(ref _gameLocationText, value);
    }

    public string UmmStatusText
    {
        get => _ummStatusText;
        private set => SetProperty(ref _ummStatusText, value);
    }

    public string PrimaryInstallText
    {
        get => _primaryInstallText;
        private set => SetProperty(ref _primaryInstallText, value);
    }

    public string InstallWarning
    {
        get => _installWarning;
        private set
        {
            if (!SetProperty(ref _installWarning, value)) return;
            RaisePropertyChanged(nameof(HasInstallWarning));
        }
    }

    public bool HasInstallWarning => !string.IsNullOrWhiteSpace(InstallWarning);

    public string LastError
    {
        get => _lastError;
        private set
        {
            if (!SetProperty(ref _lastError, value)) return;
            RaisePropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(LastError);
    public bool HasGame => _layout is not null;
    public bool CanUseGameActions => HasGame && !IsBusy;
    public bool CanInstall => HasGame && !IsBusy;
    public string? GameDirectory => _layout?.GameRoot;
    public string? ModsDirectory => _layout?.ModsPath;
    public string? UmmLogPath => _layout is null ? null : Path.Combine(_layout.ManagerPath, "Log.txt");
    public string GameLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "7th Beat Games", "A Dance of Fire and Ice", "Player.log");

    public string GameLogText
    {
        get => _gameLogText;
        private set => SetProperty(ref _gameLogText, value);
    }

    public string UmmLogText
    {
        get => _ummLogText;
        private set => SetProperty(ref _ummLogText, value);
    }

    public async Task InitializeAsync()
    {
        var saved = await ReadSavedGamePathAsync();
        await RunAsync("얼불춤 찾는 중…", async () =>
        {
            _layout = await Task.Run(() => _gameService.Detect(saved));
            await RefreshCoreAsync(includeMods: true);
        });
    }

    public async Task SelectGameAsync(string path)
    {
        await RunAsync("얼불춤 확인 중…", async () =>
        {
            var detected = await Task.Run(() => _gameService.Detect(path));
            if (detected is null)
                throw new InvalidOperationException("선택한 폴더에서 얼불춤을 찾을 수 없습니다.");
            var selectedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            var detectedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(detected.GameRoot));
            if (!detectedPath.Equals(selectedPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("선택한 폴더에서 얼불춤을 찾을 수 없습니다.");
            _layout = detected;
            await SaveGamePathAsync(detected.GameRoot);
            await RefreshCoreAsync(includeMods: true);
        });
    }

    public Task RefreshAsync(bool includeMods = true) => RunAsync("상태 확인 중…", () => RefreshCoreAsync(includeMods));

    public Task InstallAsync(bool repair = false) => RunGameActionAsync(
        repair ? "설치 문제 해결 중…" : "UMM 설치 중…",
        layout => _gameService.InstallAsync(layout, repair));

    public Task RemoveUmmAsync() => RunGameActionAsync("UMM 제거 중…", layout =>
    {
        _gameService.RemoveUmm(layout);
        return Task.CompletedTask;
    });

    public Task RestoreOriginalAsync() => RunGameActionAsync("되돌리는 중…", layout =>
    {
        _gameService.RestoreOriginal(layout);
        return Task.CompletedTask;
    });

    public async Task<ModImportPreview?> InspectModAsync(string zipPath)
    {
        if (_layout is null)
        {
            SetError("먼저 얼불춤 폴더를 선택해 주세요.");
            return null;
        }

        ModImportPreview? preview = null;
        await RunAsync("모드 확인 중…", async () =>
        {
            var inspection = await Task.Run(() => _gameService.InspectMod(_layout, zipPath));
            preview = new ModImportPreview(zipPath, inspection.Id, inspection.DisplayName,
                inspection.Version, inspection.AlreadyInstalled);
        });
        return preview;
    }

    public Task InstallModAsync(ModImportPreview preview) => RunGameActionAsync("모드 추가 중…", layout =>
    {
        _gameService.InstallMod(layout, preview.ZipPath);
        return Task.CompletedTask;
    });

    public async Task SetModEnabledAsync(ModViewItem mod, bool enabled)
    {
        var old = mod.Enabled;
        mod.Enabled = enabled;
        await RunGameActionAsync(enabled ? "모드 켜는 중…" : "모드 끄는 중…", layout =>
        {
            _gameService.SetModEnabled(layout, mod.Id, enabled);
            return Task.CompletedTask;
        });
        // RunAsync turns failures into a user-facing message so event handlers do
        // not crash. Restore the optimistic switch state when that happened.
        if (HasError)
            mod.Enabled = old;
    }

    public Task UninstallModAsync(ModViewItem mod) => RunGameActionAsync("모드 제거 중…", layout =>
    {
        _gameService.UninstallMod(layout, mod.Path);
        return Task.CompletedTask;
    });

    public Task RestoreModAsync(ModViewItem mod) => RunGameActionAsync("모드 복원 중…", layout =>
    {
        _gameService.RestoreMod(layout, mod.Path);
        return Task.CompletedTask;
    });

    public Task PermanentlyRemoveModAsync(ModViewItem mod) => RunGameActionAsync("모드 삭제 중…", layout =>
    {
        _gameService.PermanentlyRemoveMod(layout, mod.Path);
        return Task.CompletedTask;
    });

    public async Task RefreshLogsAsync()
    {
        var game = await Task.Run(GameService.ReadGameLog);
        var umm = await Task.Run(() => GameService.ReadUmmLog(_layout));
        GameLogText = string.IsNullOrWhiteSpace(game) ? "아직 게임 로그가 없습니다." : game;
        UmmLogText = string.IsNullOrWhiteSpace(umm) ? "아직 UMM 로그가 없습니다." : umm;
    }

    public void DismissError() => LastError = "";

    private async Task RunGameActionAsync(string activity, Func<GameLayout, Task> action)
    {
        if (_layout is null)
        {
            SetError("먼저 얼불춤 폴더를 선택해 주세요.");
            return;
        }

        await RunAsync(activity, async () =>
        {
            await Task.Run(() => action(_layout));
            AppendOperationLog();
            await RefreshCoreAsync(includeMods: true);
        });
    }

    private async Task RunAsync(string activity, Func<Task> action)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        Activity = activity;
        DismissError();
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            AppendOperationLog();
            SetError(FriendlyMessage(ex));
        }
        finally
        {
            Activity = "";
            IsBusy = false;
        }
    }

    private async Task RefreshCoreAsync(bool includeMods)
    {
        if (_layout is null)
        {
            _layout = await Task.Run(() => _gameService.Detect(null));
            if (_layout is null)
            {
                GameLocationText = "찾지 못함";
                UmmStatusText = "얼불춤을 먼저 선택해 주세요";
                PrimaryInstallText = "UMM 설치";
                InstallWarning = "";
                Mods.Clear();
                RaiseGameState();
                await RefreshLogsAsync();
                return;
            }
        }

        var layout = _layout;
        var status = await Task.Run(() => _gameService.ReadStatus(layout));
        GameLocationText = SteamLocator.FindGame()?.Equals(layout.GameRoot, StringComparison.OrdinalIgnoreCase) == true
            ? "Steam에서 찾음"
            : "직접 선택함";
        UmmStatusText = status.ManagerInstalled
            ? $"설치됨 · 버전 {status.ManagerVersion ?? "알 수 없음"}"
            : "설치 필요";
        PrimaryInstallText = status.ManagerInstalled ? "다시 설치" : "UMM 설치";
        InstallWarning = status.Warning ?? "";

        if (includeMods)
        {
            var mods = await Task.Run(() => _gameService.ReadMods(layout));
            Mods.Clear();
            foreach (var mod in mods)
            {
                var requirements = string.Join(", ", mod.Requirements
                    .Where(item => item.State != "OK")
                    .Select(item => $"{item.Id}: {item.State}"));
                Mods.Add(new ModViewItem
                {
                    Id = mod.Id,
                    Name = mod.DisplayName,
                    Version = mod.Version,
                    Path = mod.Path,
                    Status = mod.Status,
                    Installed = mod.Installed,
                    Enabled = mod.Enabled,
                    HomePage = mod.HomePage,
                    RequirementSummary = requirements
                });
            }
        }

        RaiseGameState();
        await RefreshLogsAsync();
    }

    private void AppendOperationLog()
    {
        foreach (var line in GameService.CollectOperationLog())
            OperationLogs.Add(new LogEntry(line.Level, line.Message, DateTimeOffset.Now));
        while (OperationLogs.Count > 500)
            OperationLogs.RemoveAt(0);
    }

    private void RaiseGameState()
    {
        RaisePropertyChanged(nameof(HasGame));
        RaisePropertyChanged(nameof(CanUseGameActions));
        RaisePropertyChanged(nameof(CanInstall));
        RaisePropertyChanged(nameof(GameDirectory));
        RaisePropertyChanged(nameof(ModsDirectory));
        RaisePropertyChanged(nameof(UmmLogPath));
    }

    private static string FriendlyMessage(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null)
            current = current.InnerException;
        return current switch
        {
            UnauthorizedAccessException => "얼불춤 폴더를 변경할 수 없습니다. Steam의 게임 폴더 권한을 확인해 주세요.",
            InvalidDataException => "파일 내용을 확인할 수 없습니다. 올바른 UMM 또는 모드 파일인지 확인해 주세요.",
            HttpRequestException => "필요한 파일을 다운로드하지 못했습니다. 인터넷 연결을 확인해 주세요.",
            _ => current.Message
        };
    }

    private void SetError(string message)
    {
        LastError = message;
        OperationLogs.Add(new LogEntry("error", message, DateTimeOffset.Now));
    }

    private static string SettingsPath => Path.Combine(AppData.Root, "settings.json");

    private static async Task<string?> ReadSavedGamePathAsync()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(SettingsPath));
            return doc.RootElement.TryGetProperty("gamePath", out var value) ? value.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static Task SaveGamePathAsync(string path) =>
        File.WriteAllTextAsync(SettingsPath, JsonSerializer.Serialize(new { gamePath = path }));
}
