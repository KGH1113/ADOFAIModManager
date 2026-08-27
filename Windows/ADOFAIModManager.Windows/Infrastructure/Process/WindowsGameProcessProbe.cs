using System.Diagnostics;
using NativeUmm.Application.Abstractions;

namespace ADOFAIModManager.Windows.Infrastructure.Process;

internal sealed class WindowsGameProcessProbe : IGameProcessProbe
{
    public bool IsRunning() =>
        System.Diagnostics.Process.GetProcessesByName("A Dance of Fire and Ice").Length > 0
        || System.Diagnostics.Process.GetProcessesByName("ADanceOfFireAndIce").Length > 0;
}
