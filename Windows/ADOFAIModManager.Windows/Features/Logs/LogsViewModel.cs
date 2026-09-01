using System.Collections.ObjectModel;
using ADOFAIModManager.Windows.Application.Abstractions;
using ADOFAIModManager.Windows.Application.Localization;
using ADOFAIModManager.Windows.Models;
using ADOFAIModManager.Windows.ViewModels;

namespace ADOFAIModManager.Windows.Features.Logs;

internal sealed class LogsViewModel : ObservableObject
{
    private readonly AppSession session;
    private readonly IWorkspaceShell workspace;
    private readonly IWindowsLogReader logReader;
    private string gameLogText = "";
    private string ummLogText = "";
    private bool gameLogIsEmpty = true;
    private bool ummLogIsEmpty = true;

    public LogsViewModel(AppSession session, IWorkspaceShell workspace, IWindowsLogReader logReader)
    {
        this.session = session;
        this.workspace = workspace;
        this.logReader = logReader;
        Localization = session.Localization;
        Localization.LanguageChanged += (_, _) =>
        {
            if (gameLogIsEmpty) GameLogText = Localization["Logs_EmptyGame"];
            if (ummLogIsEmpty) UmmLogText = Localization["Logs_EmptyUmm"];
        };
    }

    public ILocalizationService Localization { get; }
    public ObservableCollection<LogEntry> OperationLogs => session.OperationLogs;
    public string GameLogText { get => gameLogText; private set => SetProperty(ref gameLogText, value); }
    public string UmmLogText { get => ummLogText; private set => SetProperty(ref ummLogText, value); }
    public string? UmmLogPath => logReader.UmmLogPath(session.Installation);
    public string GameLogPath => logReader.GameLogPath;

    public void OpenGameLog() => workspace.RevealFile(GameLogPath);
    public void OpenUmmLog() => workspace.RevealFile(UmmLogPath);

    public async Task RefreshLogsAsync()
    {
        var game = await Task.Run(logReader.ReadGameLog);
        var umm = await Task.Run(() => logReader.ReadUmmLog(session.Installation));
        gameLogIsEmpty = string.IsNullOrWhiteSpace(game);
        ummLogIsEmpty = string.IsNullOrWhiteSpace(umm);
        GameLogText = gameLogIsEmpty ? Localization["Logs_EmptyGame"] : game;
        UmmLogText = ummLogIsEmpty ? Localization["Logs_EmptyUmm"] : umm;
        RaisePropertyChanged(nameof(UmmLogPath));
    }
}
