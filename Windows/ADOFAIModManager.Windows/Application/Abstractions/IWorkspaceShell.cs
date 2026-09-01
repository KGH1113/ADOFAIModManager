namespace ADOFAIModManager.Windows.Application.Abstractions;

internal interface IWorkspaceShell
{
    void OpenFolder(string? path);
    void RevealFile(string? path);
    bool OpenWebsite(Uri uri);
}
