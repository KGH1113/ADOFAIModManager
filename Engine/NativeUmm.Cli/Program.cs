using NativeUmm.Cli.Protocol;
using System.Text;

namespace NativeUmm.Cli;

internal static class Program
{
    public static int Main()
    {
        try
        {
            // The desktop process sends UTF-8 JSON through redirected stdin.
            // Windows console code pages must not change paths in that request.
            Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
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
