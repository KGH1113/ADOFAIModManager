using NativeUmm.Domain.Games;

namespace ADOFAIModManager.Windows.Application.Abstractions;

internal interface IWindowsLogReader
{
    string GameLogPath { get; }
    string? UmmLogPath(GameInstallation? installation);
    string ReadGameLog();
    string ReadUmmLog(GameInstallation? installation);
}
