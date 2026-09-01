using System.Collections.ObjectModel;
using ADOFAIModManager.Windows.Application.Localization;
using ADOFAIModManager.Windows.Models;
using ADOFAIModManager.Windows.Services;
using Microsoft.UI.Xaml;
using NativeUmm.Domain.Games;

namespace ADOFAIModManager.Windows.ViewModels;

internal sealed class AppSession : ObservableObject
{
    private bool isBusy;
    private string activity = "";
    private string lastError = "";
    private string? activityKey;
    private string? lastErrorKey;

    public GameService GameService { get; }
    public ILocalizationService Localization { get; }
    public IAppLanguageStore Settings { get; }
    public GameInstallation? Installation { get; set; }
    public ObservableCollection<LogEntry> OperationLogs { get; } = [];

    public AppSession(
        GameService gameService,
        ILocalizationService localization,
        IAppLanguageStore settings)
    {
        GameService = gameService;
        Localization = localization;
        Settings = settings;
        localization.LanguageChanged += (_, _) =>
        {
            if (activityKey is not null)
            {
                activity = localization[activityKey];
                RaisePropertyChanged(nameof(Activity));
            }
            if (lastErrorKey is not null)
            {
                lastError = localization[lastErrorKey];
                RaisePropertyChanged(nameof(LastError));
            }
        };
    }

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

    public async Task RunAsync(string activityResourceKey, Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        activityKey = activityResourceKey;
        Activity = Localization[activityResourceKey];
        DismissError();
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            AppendOperationLog();
            var (key, message) = FriendlyMessage(exception);
            if (key is not null) SetLocalizedError(key);
            else SetError(message);
        }
        finally
        {
            Activity = "";
            activityKey = null;
            IsBusy = false;
        }
    }

    public async Task RunGameActionAsync(string activityResourceKey, Func<GameInstallation, Task> action)
    {
        if (Installation is null)
        {
            SetLocalizedError("Error_SelectGameFirst");
            return;
        }

        await RunAsync(activityResourceKey, async () =>
        {
            await Task.Run(() => action(Installation));
            AppendOperationLog();
        });
    }

    public void DismissError()
    {
        lastErrorKey = null;
        LastError = "";
    }

    public void SetError(string message)
    {
        lastErrorKey = null;
        LastError = message;
        OperationLogs.Add(new LogEntry("error", message, DateTimeOffset.Now));
    }

    public void SetLocalizedError(string key)
    {
        lastErrorKey = key;
        SetErrorCore(Localization[key]);
    }

    private void SetErrorCore(string message)
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

    private static (string? Key, string Message) FriendlyMessage(Exception exception)
    {
        var deepest = exception;
        string? key = null;
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            deepest = current;
            key ??= current switch
            {
                GameRunningException => "Error_GameRunning",
                UnauthorizedAccessException => "Error_GameFolderPermission",
                InvalidDataException => "Error_InvalidFile",
                HttpRequestException => "Error_Download",
                _ => null
            };
        }
        return (key, deepest.Message);
    }
}
