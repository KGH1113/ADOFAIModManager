using System.Text;
using ADOFAIModManager.Windows.Application.Abstractions;
using ADOFAIModManager.Windows.Application.Localization;
using NativeUmm.Domain.Games;

namespace ADOFAIModManager.Windows.Infrastructure.Logs;

internal sealed class WindowsLogReader(ILocalizationService localization) : IWindowsLogReader
{
    public string GameLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "7th Beat Games", "A Dance of Fire and Ice", "Player.log");

    public string? UmmLogPath(GameInstallation? installation) => installation is null
        ? null : Path.Combine(installation.ManagerPath, "Log.txt");

    public string ReadGameLog() => ReadTail(GameLogPath, 30);
    public string ReadUmmLog(GameInstallation? installation) =>
        UmmLogPath(installation) is { } path ? ReadTail(path, 400) : "";

    private string ReadTail(string path, int lineCount)
    {
        if (!File.Exists(path)) return "";
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var queue = new Queue<string>(lineCount);
            while (reader.ReadLine() is { } line)
            {
                if (queue.Count == lineCount) queue.Dequeue();
                queue.Enqueue(line);
            }
            return string.Join(Environment.NewLine, queue);
        }
        catch (Exception exception)
        {
            return localization.Format("Logs_ReadError", exception.Message);
        }
    }
}
