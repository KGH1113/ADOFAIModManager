using System.Diagnostics;

using ADOFAIModManager.Windows.Application.Abstractions;

namespace ADOFAIModManager.Windows.Infrastructure.Shell;

internal sealed class WindowsWorkspaceShell : IWorkspaceShell
{
    public void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;
        System.Diagnostics.Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public void RevealFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;
        System.Diagnostics.Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
        {
            UseShellExecute = true
        });
    }

    public bool OpenWebsite(Uri uri)
    {
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
