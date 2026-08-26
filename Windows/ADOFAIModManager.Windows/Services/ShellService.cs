using System.Diagnostics;

namespace ADOFAIModManager.Windows.Services;

internal static class ShellService
{
    public static void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public static void OpenFileLocation(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
        {
            UseShellExecute = true
        });
    }
}
