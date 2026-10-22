using System.Diagnostics;
using NativeUmm.Application.Abstractions;

namespace NativeUmm.Cli.Platform;

internal sealed class GameProcessProbe : IGameProcessProbe
{
    public bool IsRunning()
    {
        if (OperatingSystem.IsLinux())
        {
            // Linux process names can be truncated; inspect full executable arguments.
            foreach (var directory in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(directory), out _)) continue;
                try {
                    var arguments = File.ReadAllText(Path.Combine(directory, "cmdline")).Split('\0');
                    if (arguments.Any(argument => Path.GetFileName(argument.Replace('\\', '/')) is
                        "ADanceOfFireAndIce" or "ADanceOfFireAndIce.x86_64" or "A Dance of Fire and Ice.exe")) return true;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        foreach (var name in new[] { "ADanceOfFireAndIce", "A Dance of Fire and Ice" })
        {
            var processes = Process.GetProcessesByName(name);
            try { if (processes.Length > 0) return true; }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        return false;
    }
}
