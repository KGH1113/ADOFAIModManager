using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;
using NativeUmm.Domain.Installation;

namespace NativeUmm.Infrastructure.Installation;

public sealed class DnlibUmmInstaller(IOperationLog log, IAppDataPaths appData) : IUmmInstaller
{
    private readonly IReadOnlyList<string>? steamClientRoots;

    internal DnlibUmmInstaller(IOperationLog log, IAppDataPaths appData, IReadOnlyList<string> steamClientRoots)
        : this(log, appData)
    {
        this.steamClientRoots = steamClientRoots;
    }

    public InstallStatus ReadStatus(GameInstallation installation) =>
        new Installer(installation, log, appData, steamClientRoots).ReadStatus();

    public Task InstallAsync(GameInstallation installation, string? payloadDirectory, bool forcePayload) =>
        new Installer(installation, log, appData, steamClientRoots).InstallAsync(payloadDirectory, forcePayload);

    public void RemoveHook(GameInstallation installation) =>
        new Installer(installation, log, appData, steamClientRoots).RemoveHook();

    public void RestoreOriginal(GameInstallation installation) =>
        new Installer(installation, log, appData, steamClientRoots).RestoreOriginal();
}
