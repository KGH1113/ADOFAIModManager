using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;
using NativeUmm.Domain.Installation;

namespace NativeUmm.Infrastructure.Installation;

public sealed class DnlibUmmInstaller(IOperationLog log, IAppDataPaths appData) : IUmmInstaller
{
    public InstallStatus ReadStatus(GameInstallation installation) =>
        new Installer(installation, log, appData).ReadStatus();

    public Task InstallAsync(GameInstallation installation, string? payloadDirectory, bool forcePayload) =>
        new Installer(installation, log, appData).InstallAsync(payloadDirectory, forcePayload);

    public void RemoveHook(GameInstallation installation) =>
        new Installer(installation, log, appData).RemoveHook();

    public void RestoreOriginal(GameInstallation installation) =>
        new Installer(installation, log, appData).RestoreOriginal();
}
