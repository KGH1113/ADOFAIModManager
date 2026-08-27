using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ADOFAIModManager.Windows.Infrastructure.Installation;

internal static class UserInstallation
{
    private const string AppName = "ADOFAI Mod Manager";
    private const string ExeName = "ADOFAIModManager.Windows.exe";

    public static string InstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "ADOFAIModManager");

    public static string InstalledExePath => Path.Combine(InstallDirectory, ExeName);

    public static bool HandleMaintenanceArguments()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
        {
            BeginUninstall();
            return true;
        }

        var cleanupIndex = Array.FindIndex(args, item => item.Equals("--cleanup", StringComparison.OrdinalIgnoreCase));
        if (cleanupIndex >= 0 && cleanupIndex + 2 < args.Length)
        {
            var directory = args[cleanupIndex + 1];
            _ = int.TryParse(args[cleanupIndex + 2], out var processId);
            CleanupAfterExit(directory, processId);
            return true;
        }
        return false;
    }

    public static bool InstallAndRelaunchIfNeeded()
    {
        if (Debugger.IsAttached || Environment.GetCommandLineArgs().Contains("--portable"))
            return false;

        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExe))
            return false;
        if (File.Exists(Path.ChangeExtension(currentExe, ".dll")))
            return false; // Normal framework build: only published single-file builds self-install.
        if (Path.GetFullPath(currentExe).Equals(Path.GetFullPath(InstalledExePath), StringComparison.OrdinalIgnoreCase))
        {
            RegisterCurrentInstallation();
            return false;
        }

        Directory.CreateDirectory(InstallDirectory);
        File.Copy(currentExe, InstalledExePath, overwrite: true);
        RegisterCurrentInstallation();
        System.Diagnostics.Process.Start(new ProcessStartInfo(InstalledExePath, "--installed") { UseShellExecute = true });
        return true;
    }

    private static void RegisterCurrentInstallation()
    {
        RegisterUninstallEntry();
        CreateStartMenuShortcut();
    }

    private static void RegisterUninstallEntry()
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ADOFAIModManager");
        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", typeof(UserInstallation).Assembly.GetName().Version?.ToString(3) ?? "0.1.0");
        key.SetValue("Publisher", "ADOFAI Mod Manager contributors");
        key.SetValue("InstallLocation", InstallDirectory);
        key.SetValue("DisplayIcon", InstalledExePath);
        key.SetValue("UninstallString", $"\"{InstalledExePath}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void CreateStartMenuShortcut()
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        Directory.CreateDirectory(programs);
        var shortcutPath = Path.Combine(programs, $"{AppName}.lnk");
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(InstalledExePath);
        link.SetWorkingDirectory(InstallDirectory);
        link.SetDescription("얼불춤 UMM 및 모드 관리");
        ((IPersistFile)link).Save(shortcutPath, true);
    }

    private static void BeginUninstall()
    {
        try
        {
            FileAssociationRegistrar.Unregister();
        }
        catch { }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ADOFAIModManager", false);
        }
        catch { }

        try
        {
            var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), $"{AppName}.lnk");
            File.Delete(shortcut);
        }
        catch { }

        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExe))
            return;
        var temporary = Path.Combine(Path.GetTempPath(), $"adofai-mod-manager-cleanup-{Guid.NewGuid():N}.exe");
        File.Copy(currentExe, temporary, overwrite: true);
        System.Diagnostics.Process.Start(new ProcessStartInfo(temporary,
            $"--cleanup \"{InstallDirectory}\" {Environment.ProcessId}")
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static void CleanupAfterExit(string directory, int processId)
    {
        try
        {
            if (processId > 0)
                System.Diagnostics.Process.GetProcessById(processId).WaitForExit(15_000);
        }
        catch { }

        try
        {
            if (Path.GetFullPath(directory).Equals(Path.GetFullPath(InstallDirectory), StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch { }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder file, int maxPath, nint data, uint flags);
        void GetIDList(out nint pidl);
        void SetIDList(nint pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder name, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder dir, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder args, int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder iconPath, int iconPathLength, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(nint hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }
}
