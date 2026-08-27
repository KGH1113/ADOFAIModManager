using NativeUmm.Domain.Games;
using NativeUmm.Domain.Installation;
using NativeUmm.Domain.Mods;

namespace NativeUmm.Application.Abstractions;

public interface IGameLocator
{
    GameInstallation? Locate(string? requestedPath);
}

public interface IGameProcessProbe
{
    bool IsRunning();
}

public interface IArchitectureReader
{
    string Read(GameInstallation installation);
}

public interface IUmmInstaller
{
    InstallStatus ReadStatus(GameInstallation installation);
    Task InstallAsync(GameInstallation installation, string? payloadDirectory, bool forcePayload);
    void RemoveHook(GameInstallation installation);
    void RestoreOriginal(GameInstallation installation);
}

public interface IModService
{
    List<ModInfo> List(GameInstallation installation);
    ModInspection Inspect(GameInstallation installation, string zipPath);
    void Install(GameInstallation installation, string zipPath);
    Task InstallFromUrlAsync(GameInstallation installation, string url);
    void SetEnabled(GameInstallation installation, string modId, bool enabled);
    void Uninstall(GameInstallation installation, string path);
    void Restore(GameInstallation installation, string path);
    void Remove(GameInstallation installation, string path);
}

public interface IOperationLog
{
    void Begin();
    void Info(string message);
    void Warn(string message);
    void Fail(string message);
    IReadOnlyList<OperationLogLine> Collect();
}

public readonly record struct OperationLogLine(string Level, string Message);

public interface IAppDataPaths
{
    string Cache { get; }
    string RemovedMods { get; }
    string RemovedUmm { get; }
}
