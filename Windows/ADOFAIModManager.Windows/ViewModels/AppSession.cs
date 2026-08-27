using System.Collections.ObjectModel;
using ADOFAIModManager.Windows.Models;
using ADOFAIModManager.Windows.Services;
using Microsoft.UI.Xaml;
using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;

namespace ADOFAIModManager.Windows.ViewModels;

internal sealed class AppSession(GameService gameService, IAppDataPaths appData) : ObservableObject
{
    private bool isBusy;
    private string activity = "";
    private string lastError = "";

    public GameService GameService { get; } = gameService;
    public GameInstallation? Installation { get; set; }
    public string SettingsPath => Path.Combine(Path.GetDirectoryName(appData.Cache)!, "settings.json");
    public ObservableCollection<LogEntry> OperationLogs { get; } = [];

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (!SetProperty(ref isBusy, value)) return;
            RaisePropertyChanged(nameof(BusyVisibility));
        }
    }

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;
    public string Activity { get => activity; private set => SetProperty(ref activity, value); }
    public string LastError
    {
        get => lastError;
        private set
        {
            if (!SetProperty(ref lastError, value)) return;
            RaisePropertyChanged(nameof(HasError));
        }
    }
    public bool HasError => !string.IsNullOrWhiteSpace(LastError);

    public async Task RunAsync(string activityText, Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        Activity = activityText;
        DismissError();
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            AppendOperationLog();
            SetError(FriendlyMessage(exception));
        }
        finally
        {
            Activity = "";
            IsBusy = false;
        }
    }

    public async Task RunGameActionAsync(string activityText, Func<GameInstallation, Task> action)
    {
        if (Installation is null)
        {
            SetError("먼저 얼불춤 폴더를 선택해 주세요.");
            return;
        }

        await RunAsync(activityText, async () =>
        {
            await Task.Run(() => action(Installation));
            AppendOperationLog();
        });
    }

    public void DismissError() => LastError = "";

    public void SetError(string message)
    {
        LastError = message;
        OperationLogs.Add(new LogEntry("error", message, DateTimeOffset.Now));
    }

    private void AppendOperationLog()
    {
        foreach (var line in GameService.CollectOperationLog())
            OperationLogs.Add(new LogEntry(line.Level, line.Message, DateTimeOffset.Now));
        while (OperationLogs.Count > 500)
            OperationLogs.RemoveAt(0);
    }

    private static string FriendlyMessage(Exception exception)
    {
        var current = exception;
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
}
