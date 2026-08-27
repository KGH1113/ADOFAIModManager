namespace NativeUmm.Domain.Games;

public sealed record GameInstallation(
    string GameRoot,
    string AppPath,
    string ManagedPath,
    string EntryAssemblyPath,
    string ManagerPath,
    string ModsPath,
    string OriginalBackupPath);
