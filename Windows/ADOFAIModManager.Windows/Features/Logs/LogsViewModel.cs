using System.Collections.ObjectModel;
using ADOFAIModManager.Windows.Application.Abstractions;
using ADOFAIModManager.Windows.Models;
using ADOFAIModManager.Windows.ViewModels;

namespace ADOFAIModManager.Windows.Features.Logs;

internal sealed class LogsViewModel(
    AppSession session,
    IWorkspaceShell workspace,
    IWindowsLogReader logReader) : ObservableObject
{
    private string gameLogText = "";
    private string ummLogText = "";

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
        GameLogText = string.IsNullOrWhiteSpace(game) ? "아직 게임 로그가 없습니다." : game;
        UmmLogText = string.IsNullOrWhiteSpace(umm) ? "아직 UMM 로그가 없습니다." : umm;
        RaisePropertyChanged(nameof(UmmLogPath));
    }
}
