using NativeUmm.Cli.Platform.Mac;
using NativeUmm.Cli.Platform.Windows;
using NativeUmm.Cli.Platform.Linux;
using NativeUmm.Cli.Platform;
using NativeUmm.Application.Abstractions;
using NativeUmm.Infrastructure.Presets;
using NativeUmm.Cli.Protocol;
using NativeUmm.Infrastructure.Installation;
using NativeUmm.Infrastructure.Logging;
using NativeUmm.Infrastructure.Mods;
using NativeUmm.Infrastructure.Storage;

namespace NativeUmm.Cli;

internal static class CompositionRoot
{
    public static EngineRequestHandler RequestHandler { get; } = BuildRequestHandler();

    private static EngineRequestHandler BuildRequestHandler()
    {
        var log = new BufferedOperationLog();
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("ADOFAI Mod Manager supports macOS, Windows and Linux.");
        var appData = OperatingSystem.IsWindows() ? AppDataPaths.ForWindows()
            : OperatingSystem.IsLinux() ? AppDataPaths.ForLinux() : AppDataPaths.ForMacOS();
        IGameLocator locator = OperatingSystem.IsWindows() ? new WindowsGameLocator()
            : OperatingSystem.IsLinux() ? new LinuxGameLocator() : new MacGameLocator();
        return new EngineRequestHandler(
            locator,
            (IArchitectureReader)locator,
            new DnlibUmmInstaller(log, appData),
            new FileModService(log, appData),
            log,
            new GameProcessProbe(),
            new ModPresetService());
    }
}
