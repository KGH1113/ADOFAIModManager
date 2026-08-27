using NativeUmm.Cli.Protocol;

namespace NativeUmm.Cli;

internal static class Program
{
    public static int Main()
    {
        try
        {
            var request = Console.In.ReadToEnd();
            Console.Out.Write(CompositionRoot.RequestHandler.Handle(
                string.IsNullOrWhiteSpace(request) ? "{}" : request));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Out.Write(EngineRequestHandler.ErrorJson(exception.Message));
            return 1;
        }
    }
}
