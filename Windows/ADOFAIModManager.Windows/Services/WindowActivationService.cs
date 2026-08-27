using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace ADOFAIModManager.Windows.Services;

internal static class WindowActivationService
{
    private const int ShowWindowRestore = 9;

    public static void BringProcessWindowToFront(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            process.Refresh();
            BringHandleToFront(process.MainWindowHandle);
        }
        catch
        {
            // Activation redirection still works if Windows denies foreground access.
        }
    }

    public static void BringWindowToFront(Window window)
    {
        window.Activate();
        BringHandleToFront(WindowNative.GetWindowHandle(window));
    }

    private static void BringHandleToFront(nint windowHandle)
    {
        if (windowHandle == 0)
            return;
        _ = ShowWindow(windowHandle, ShowWindowRestore);
        _ = SetForegroundWindow(windowHandle);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint windowHandle);
}
