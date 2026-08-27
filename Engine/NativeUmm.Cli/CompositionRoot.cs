using NativeUmm.Cli.Platform.Mac;
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
        var appData = AppDataPaths.ForMacOS();
        var locator = new MacGameLocator();
        return new EngineRequestHandler(
            locator,
            locator,
            new DnlibUmmInstaller(log, appData),
            new FileModService(log, appData),
            log);
    }
}
