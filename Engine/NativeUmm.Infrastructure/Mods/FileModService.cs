using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;
using NativeUmm.Domain.Mods;

namespace NativeUmm.Infrastructure.Mods;

public sealed class FileModService(IOperationLog log, IAppDataPaths appData) : IModService
{
    public List<ModInfo> List(GameInstallation installation) => ModManager.List(installation);
    public ModInspection Inspect(GameInstallation installation, string zipPath) => ModManager.Inspect(installation, zipPath);
    public void Install(GameInstallation installation, string zipPath) => ModManager.Install(installation, zipPath, log, appData);
    public Task InstallFromUrlAsync(GameInstallation installation, string url) => ModManager.InstallFromUrlAsync(installation, url, log, appData);
    public void SetEnabled(GameInstallation installation, string modId, bool enabled) => ModManager.SetEnabled(installation, modId, enabled, log);
    public void Uninstall(GameInstallation installation, string path) => ModManager.Uninstall(installation, path, log, appData);
    public void Restore(GameInstallation installation, string path) => ModManager.Restore(installation, path, log, appData);
    public void Remove(GameInstallation installation, string path) => ModManager.Remove(installation, path, log, appData);
}
