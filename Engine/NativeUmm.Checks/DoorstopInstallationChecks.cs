using dnlib.DotNet;
using dnlib.DotNet.Emit;
using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;
using NativeUmm.Domain.Installation;
using NativeUmm.Infrastructure.Installation;
using DnMethodAttributes = dnlib.DotNet.MethodAttributes;
using DnMethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using DnTypeAttributes = dnlib.DotNet.TypeAttributes;

internal static class DoorstopInstallationChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        var windows = WindowsGame(Path.Combine(root, "doorstop-windows"));
        var ummTarget = @"A Dance of Fire and Ice_Data\Managed\UnityModManager\UnityModManager.dll";
        WriteAssembly(windows.EntryAssemblyPath, hooked: false);
        PlaceManager(windows);
        WriteWindowsDoorstop(windows.GameRoot, "true", ummTarget, version: true);
        var active = Read(windows);
        check(active.Injection == InjectionMethod.Doorstop && active.Injection.ToProtocol() == "doorstop",
            "Active Windows Doorstop targeting UnityModManager is installed");
        check(!active.HookInstalled && active.ManagerInstalled && Readable(active),
            "Doorstop install does not require an assembly patch");

        WriteWindowsDoorstop(windows.GameRoot, "TRUE", ummTarget, version: true);
        check(Read(windows).Injection == InjectionMethod.Doorstop, "Windows enabled=TRUE is on, matching Doorstop's case-insensitive parser");
        WriteWindowsDoorstop(windows.GameRoot, "false", ummTarget, version: true);
        check(Read(windows).Injection == InjectionMethod.None, "Windows enabled=false is not an active Doorstop install");
        WriteWindowsDoorstop(windows.GameRoot, "1", ummTarget, version: false);
        check(Read(windows).Injection == InjectionMethod.None, "Windows enabled=1 is not true; Doorstop only accepts true");
        File.WriteAllText(Path.Combine(windows.GameRoot, "doorstop_config.ini"), "[General]\r\ntarget_assembly = " + ummTarget + "\r\n");
        check(Read(windows).Injection == InjectionMethod.Doorstop, "A missing Windows enabled key defaults to true");

        var absolute = Path.GetFullPath(Path.Combine(windows.ManagerPath, "UnityModManager.dll"));
        WriteWindowsDoorstop(windows.GameRoot, "true", absolute, version: false);
        check(Read(windows).Injection == InjectionMethod.Doorstop, "An absolute Doorstop target resolves to UnityModManager.dll");
        var dotted = @"A Dance of Fire and Ice_Data\Managed\UnityModManager\..\UnityModManager\UnityModManager.dll";
        WriteWindowsDoorstop(windows.GameRoot, "true", dotted, version: false);
        check(Read(windows).Injection == InjectionMethod.Doorstop, "A relative Doorstop target with parent segments resolves to UnityModManager.dll");
        WriteWindowsDoorstop(windows.GameRoot, "true", ummTarget.ToLowerInvariant(), version: false);
        check(Read(windows).Injection == InjectionMethod.Doorstop, "Windows Doorstop paths are compared case-insensitively");
        CheckQuotedIni(root, ummTarget, '"', check);
        CheckQuotedIni(root, ummTarget, '\'', check);
        if (!OperatingSystem.IsWindows())
            CheckProtonTargets(root, check);

        File.Delete(Path.Combine(windows.GameRoot, "winhttp.dll"));
        WriteWindowsDoorstop(windows.GameRoot, "true", ummTarget, version: false);
        File.Delete(Path.Combine(windows.GameRoot, "winhttp.dll"));
        check(Read(windows).Injection == InjectionMethod.None, "A Doorstop config without winhttp.dll is not injecting");

        WriteWindowsDoorstop(windows.GameRoot, "true", @"BepInEx\core\BepInEx.Preloader.dll", version: true);
        var notes = Path.Combine(windows.GameRoot, "notes.txt");
        File.WriteAllText(notes, "keep");
        var bepDll = File.ReadAllBytes(Path.Combine(windows.GameRoot, "winhttp.dll"));
        var bepIni = File.ReadAllText(Path.Combine(windows.GameRoot, "doorstop_config.ini"));
        check(Read(windows).Injection == InjectionMethod.None, "Doorstop pointing at BepInEx is not a UMM install");
        InstallerFor(root).RemoveHook(windows);
        check(File.ReadAllBytes(Path.Combine(windows.GameRoot, "winhttp.dll")).AsSpan().SequenceEqual(bepDll) &&
              File.ReadAllText(Path.Combine(windows.GameRoot, "doorstop_config.ini")) == bepIni &&
              File.Exists(Path.Combine(windows.GameRoot, ".doorstop_version")) &&
              File.ReadAllText(notes) == "keep" &&
              !Backups(windows.GameRoot, "winhttp.dll").Any(),
            "Uninstall leaves a BepInEx Doorstop deployment untouched");

        WriteWindowsDoorstop(windows.GameRoot, "false", ummTarget, version: true);
        var beforeCore = File.ReadAllBytes(windows.EntryAssemblyPath);
        InstallerFor(root).RemoveHook(windows);
        check(Read(windows).Injection == InjectionMethod.None &&
              !File.Exists(Path.Combine(windows.GameRoot, "winhttp.dll")) &&
              !File.Exists(Path.Combine(windows.GameRoot, "doorstop_config.ini")) &&
              !File.Exists(Path.Combine(windows.GameRoot, ".doorstop_version")) &&
              Backups(windows.GameRoot, "winhttp.dll").Any() &&
              Backups(windows.GameRoot, "doorstop_config.ini").Any() &&
              File.ReadAllBytes(windows.EntryAssemblyPath).AsSpan().SequenceEqual(beforeCore) &&
              File.ReadAllText(notes) == "keep",
            "Uninstall removes a disabled UMM Doorstop deployment after backing it up");

        WriteAssembly(windows.EntryAssemblyPath, hooked: true);
        WriteWindowsDoorstop(windows.GameRoot, "true", ummTarget, version: true);
        var both = Read(windows);
        check(both.Injection == InjectionMethod.Both && both.Injection.ToProtocol() == "both" && both.HookInstalled,
            "Assembly patch and active UMM Doorstop are both reported");
        InstallerFor(root).RemoveHook(windows);
        var cleared = Read(windows);
        check(cleared.Injection == InjectionMethod.None && !cleared.HookInstalled && Readable(cleared) &&
              !File.Exists(Path.Combine(windows.GameRoot, "winhttp.dll")) &&
              !StarterRemains(windows.EntryAssemblyPath),
            "Uninstall removes both the assembly starter and UMM Doorstop files");

        WriteAssembly(windows.EntryAssemblyPath, hooked: true);
        WriteWindowsDoorstop(windows.GameRoot, "true", @"BepInEx\core\BepInEx.Preloader.dll", version: true);
        check(Read(windows).Injection == InjectionMethod.Assembly, "A BepInEx Doorstop beside an assembly patch is not Both");
        var bepAgain = File.ReadAllBytes(Path.Combine(windows.GameRoot, "winhttp.dll"));
        InstallerFor(root).RemoveHook(windows);
        check(Read(windows).Injection == InjectionMethod.None &&
              File.ReadAllBytes(Path.Combine(windows.GameRoot, "winhttp.dll")).AsSpan().SequenceEqual(bepAgain),
            "Uninstall of an assembly patch leaves the BepInEx Doorstop in place");

        var linux = LinuxGame(Path.Combine(root, "doorstop-linux"));
        WriteAssembly(linux.EntryAssemblyPath, hooked: false);
        PlaceManager(linux);
        var linuxTarget = "ADanceOfFireAndIce_Data/Managed/UnityModManager/UnityModManager.dll";
        WriteUnixDoorstop(linux.GameRoot, "libdoorstop.so", "1", linuxTarget);
        check(Read(linux).Injection == InjectionMethod.None,
            "Linux run.sh and libdoorstop.so are not active when the game binary is launched directly");
        WriteUnixDoorstop(linux.GameRoot, "libdoorstop.so", "0", linuxTarget);
        check(Read(linux).Injection == InjectionMethod.None, "Linux enabled=0 is not an active Doorstop install");
        WriteUnixDoorstop(linux.GameRoot, "libdoorstop.so", "true", linuxTarget);
        check(Read(linux).Injection == InjectionMethod.None, "Linux enabled=true is not 1, so Doorstop stays off");
        WriteUnixDoorstop(linux.GameRoot, "libdoorstop.so", "1", linuxTarget.ToUpperInvariant());
        check(Read(linux).Injection == InjectionMethod.None, "Linux Doorstop paths are case-sensitive");
        WriteUnixDoorstop(linux.GameRoot, "libdoorstop.so", "1", "BepInEx/core/BepInEx.Preloader.dll");
        var linuxLibrary = File.ReadAllBytes(Path.Combine(linux.GameRoot, "libdoorstop.so"));
        check(Read(linux).Injection == InjectionMethod.None, "Linux Doorstop pointing at BepInEx is not a UMM install");
        InstallerFor(root).RemoveHook(linux);
        check(File.Exists(Path.Combine(linux.GameRoot, "run.sh")) &&
              File.ReadAllBytes(Path.Combine(linux.GameRoot, "libdoorstop.so")).AsSpan().SequenceEqual(linuxLibrary),
            "Uninstall leaves a Linux BepInEx Doorstop untouched");
        File.WriteAllText(Path.Combine(linux.GameRoot, "run.sh"),
            "#!/bin/sh\nenabled=\"1\"\nenabled=\"0\"\ntarget_assembly=\"" + linuxTarget + "\"\n");
        check(Read(linux).Injection == InjectionMethod.None, "The last literal enabled assignment in run.sh wins");

        var mac = MacGame(Path.Combine(root, "doorstop-mac"));
        WriteAssembly(mac.EntryAssemblyPath, hooked: false);
        PlaceManager(mac);
        var macTarget = "ADanceOfFireAndIce.app/Contents/Resources/Data/Managed/UnityModManager/UnityModManager.dll";
        WriteUnixDoorstop(mac.GameRoot, "libdoorstop.dylib", "1", macTarget.Replace("Managed", "managed"));
        check(Read(mac).Injection == InjectionMethod.None, "macOS run.sh is not active when the .app is launched directly");
        File.WriteAllText(Path.Combine(mac.GameRoot, "ADanceOfFireAndIce.desktop"),
            "[Desktop Entry]\nType=Application\nExec=\"" + Path.Combine(mac.GameRoot, "run.sh") + "\"\n");
        check(Read(mac).Injection == InjectionMethod.Doorstop, "macOS Doorstop is active when a launcher starts run.sh");
        File.Delete(Path.Combine(mac.GameRoot, "libdoorstop.dylib"));
        check(Read(mac).Injection == InjectionMethod.None, "macOS Doorstop without libdoorstop.dylib is not injecting");

        var repair = WindowsGame(Path.Combine(root, "doorstop-repair"));
        WriteAssembly(repair.EntryAssemblyPath, hooked: false);
        var original = File.ReadAllBytes(repair.EntryAssemblyPath);
        WriteWindowsDoorstop(repair.GameRoot, "true", ummTarget, version: true);
        var log = new TestLog();
        new DnlibUmmInstaller(log, new TestAppData(root)).InstallAsync(repair, PayloadDirectory(), forcePayload: false)
            .GetAwaiter().GetResult();
        var repaired = Read(repair);
        check(File.ReadAllBytes(repair.EntryAssemblyPath).AsSpan().SequenceEqual(original) &&
              repaired.Injection == InjectionMethod.Doorstop && !repaired.HookInstalled && repaired.ManagerInstalled &&
              repaired.ManagerVersion is not null && repaired.ManagerVersion != "unreadable" &&
              File.Exists(Path.Combine(repair.ManagerPath, "Config.xml")) &&
              !File.Exists(repair.OriginalBackupPath) &&
              log.Collect().Any(line => line.Message.Contains("left UnityEngine.CoreModule.dll unchanged", StringComparison.Ordinal)),
            "Install updates UMM files and does not patch UnityEngine.CoreModule when Doorstop is active");

        var again = WindowsGame(Path.Combine(root, "doorstop-already-patched"));
        WriteAssembly(again.EntryAssemblyPath, hooked: true);
        var patched = File.ReadAllBytes(again.EntryAssemblyPath);
        WriteWindowsDoorstop(again.GameRoot, "true", ummTarget, version: true);
        PlaceManager(again);
        new DnlibUmmInstaller(new TestLog(), new TestAppData(root)).InstallAsync(again, PayloadDirectory(), forcePayload: true)
            .GetAwaiter().GetResult();
        check(File.ReadAllBytes(again.EntryAssemblyPath).AsSpan().SequenceEqual(patched) &&
              Read(again).Injection == InjectionMethod.Both && Read(again).HookInstalled,
            "Install does not rewrite an assembly patch that is already beside an active Doorstop");

        var alongside = WindowsGame(Path.Combine(root, "doorstop-bepinex-install"));
        WriteAssembly(alongside.EntryAssemblyPath, hooked: false);
        WriteWindowsDoorstop(alongside.GameRoot, "true", @"BepInEx\core\BepInEx.Preloader.dll", version: true);
        var foreign = File.ReadAllBytes(Path.Combine(alongside.GameRoot, "winhttp.dll"));
        new DnlibUmmInstaller(new TestLog(), new TestAppData(root)).InstallAsync(alongside, PayloadDirectory(), forcePayload: false)
            .GetAwaiter().GetResult();
        check(Read(alongside).Injection == InjectionMethod.Assembly &&
              File.ReadAllBytes(Path.Combine(alongside.GameRoot, "winhttp.dll")).AsSpan().SequenceEqual(foreign),
            "Install still patches the assembly when Doorstop points at BepInEx and leaves those files alone");

        var disabled = WindowsGame(Path.Combine(root, "doorstop-disabled-install"));
        WriteAssembly(disabled.EntryAssemblyPath, hooked: false);
        WriteWindowsDoorstop(disabled.GameRoot, "false", ummTarget, version: true);
        new DnlibUmmInstaller(new TestLog(), new TestAppData(root)).InstallAsync(disabled, PayloadDirectory(), forcePayload: false)
            .GetAwaiter().GetResult();
        check(Read(disabled).Injection == InjectionMethod.Assembly &&
              File.Exists(Path.Combine(disabled.GameRoot, "winhttp.dll")) &&
              File.ReadAllText(Path.Combine(disabled.GameRoot, "doorstop_config.ini")).Contains("enabled = false", StringComparison.Ordinal),
            "Install patches the assembly when UMM Doorstop is disabled");

        var direct = LinuxGame(Path.Combine(root, "doorstop-linux-direct"));
        WriteAssembly(direct.EntryAssemblyPath, hooked: false);
        var directBefore = File.ReadAllBytes(direct.EntryAssemblyPath);
        WriteUnixDoorstop(direct.GameRoot, "libdoorstop.so", "1", "ADanceOfFireAndIce_Data/Managed/UnityModManager/UnityModManager.dll");
        new DnlibUmmInstaller(new TestLog(), new TestAppData(root)).InstallAsync(direct, PayloadDirectory(), forcePayload: false)
            .GetAwaiter().GetResult();
        var directStatus = Read(direct);
        check(directStatus.Injection == InjectionMethod.Assembly && directStatus.HookInstalled &&
              !File.ReadAllBytes(direct.EntryAssemblyPath).AsSpan().SequenceEqual(directBefore) &&
              File.Exists(Path.Combine(direct.GameRoot, "run.sh")) &&
              File.Exists(Path.Combine(direct.GameRoot, "libdoorstop.so")),
            "Install still patches the assembly when Unix Doorstop is not the launch path");

        var launchedRoot = Path.Combine(root, "steam-linux", "steamapps", "common", "ADanceOfFireAndIce");
        var launched = LinuxGame(launchedRoot);
        WriteAssembly(launched.EntryAssemblyPath, hooked: false);
        var launchedBefore = File.ReadAllBytes(launched.EntryAssemblyPath);
        WriteUnixDoorstop(launched.GameRoot, "libdoorstop.so", "1", "ADanceOfFireAndIce_Data/Managed/UnityModManager/UnityModManager.dll");
        var steamClient = Path.Combine(root, "steam-linux");
        WriteLaunchOptions(steamClient, """
            "977950"
            {
                "LaunchOptions" "%command%"
            }
            "123"
            {
                "LaunchOptions" "./run.sh %command%"
            }
            """);
        WithSteamClient(steamClient, () =>
        {
            check(Read(launched).Injection == InjectionMethod.None, "Another game's run.sh launch option does not activate Doorstop");
            WriteLaunchOptions(steamClient, """
                "977950"
                {
                    "LaunchOptions" "./run.sh %command%"
                }
                """);
            check(Read(launched).Injection == InjectionMethod.Doorstop, "Steam LaunchOptions that run run.sh make Linux Doorstop active");
            new DnlibUmmInstaller(new TestLog(), new TestAppData(root)).InstallAsync(launched, PayloadDirectory(), forcePayload: false)
                .GetAwaiter().GetResult();
            check(File.ReadAllBytes(launched.EntryAssemblyPath).AsSpan().SequenceEqual(launchedBefore) &&
                  Read(launched).Injection == InjectionMethod.Doorstop && !Read(launched).HookInstalled,
                "Install does not patch the assembly when Steam launches the game through run.sh");
        });

        var secondaryRoot = Path.Combine(root, "secondary-library");
        var secondary = LinuxGame(Path.Combine(secondaryRoot, "steamapps", "common", "ADanceOfFireAndIce"));
        WriteAssembly(secondary.EntryAssemblyPath, hooked: false);
        var secondaryBefore = File.ReadAllBytes(secondary.EntryAssemblyPath);
        WriteUnixDoorstop(secondary.GameRoot, "libdoorstop.so", "1", "ADanceOfFireAndIce_Data/Managed/UnityModManager/UnityModManager.dll");
        WriteLaunchOptions(secondaryRoot, """
            "977950"
            {
                "LaunchOptions" "./run.sh %command%"
            }
            """);
        check(Read(secondary).Injection == InjectionMethod.None,
            "LaunchOptions stored beside a secondary Steam library are not the client config");
        var primary = Path.Combine(root, "primary-steam");
        WriteLaunchOptions(primary, """
            "977950"
            {
                "LaunchOptions" "./run.sh %command%"
            }
            """);
        WithSteamClient(primary, () =>
        {
            check(Read(secondary).Injection == InjectionMethod.Doorstop,
                "A secondary library uses LaunchOptions from the primary Steam install");
            new DnlibUmmInstaller(new TestLog(), new TestAppData(root)).InstallAsync(secondary, PayloadDirectory(), forcePayload: true)
                .GetAwaiter().GetResult();
            check(File.ReadAllBytes(secondary.EntryAssemblyPath).AsSpan().SequenceEqual(secondaryBefore) &&
                  !Read(secondary).HookInstalled,
                "Install does not add an assembly hook when the primary Steam config launches run.sh");
        });

        var desktopGame = LinuxGame(Path.Combine(root, "doorstop-desktop-leftover"));
        WriteAssembly(desktopGame.EntryAssemblyPath, hooked: false);
        var desktopBefore = File.ReadAllBytes(desktopGame.EntryAssemblyPath);
        WriteUnixDoorstop(desktopGame.GameRoot, "libdoorstop.so", "1", "ADanceOfFireAndIce_Data/Managed/UnityModManager/UnityModManager.dll");
        File.WriteAllText(Path.Combine(desktopGame.GameRoot, "ADanceOfFireAndIce.desktop"),
            "[Desktop Entry]\nType=Application\nExec=\"" + Path.Combine(desktopGame.GameRoot, "run.sh") + "\"\n");
        var directClient = Path.Combine(root, "primary-steam-direct");
        WriteLaunchOptions(directClient, """
            "977950"
            {
                "LaunchOptions" "%command%"
            }
            """);
        WithSteamClient(directClient, () =>
        {
            check(Read(desktopGame).Injection == InjectionMethod.None,
                "A leftover .desktop run.sh launcher does not override Steam's %command%");
            new DnlibUmmInstaller(new TestLog(), new TestAppData(root)).InstallAsync(desktopGame, PayloadDirectory(), forcePayload: false)
                .GetAwaiter().GetResult();
            var afterDesktop = Read(desktopGame);
            check(afterDesktop.Injection == InjectionMethod.Assembly && afterDesktop.HookInstalled &&
                  !File.ReadAllBytes(desktopGame.EntryAssemblyPath).AsSpan().SequenceEqual(desktopBefore),
                "Install patches the assembly when Steam launches directly despite a .desktop file");
        });

        var wrapped = LinuxGame(Path.Combine(root, "doorstop-linux-wrapper"));
        WriteAssembly(wrapped.EntryAssemblyPath, hooked: false);
        PlaceManager(wrapped);
        WriteUnixDoorstop(wrapped.GameRoot, "libdoorstop.so", "1", "ADanceOfFireAndIce_Data/Managed/UnityModManager/UnityModManager.dll");
        File.WriteAllText(wrapped.AppPath, "#!/bin/sh\nexport LD_PRELOAD=\"libdoorstop.so\"\nexec ./ADanceOfFireAndIce.real \"$@\"\n");
        check(Read(wrapped).Injection == InjectionMethod.Doorstop, "A game executable that preloads libdoorstop is an active Doorstop launch");
    }

    private static void CheckQuotedIni(string root, string target, char quote, Action<bool, string> check)
    {
        var game = WindowsGame(Path.Combine(root, "doorstop-quoted-" + (quote == '"' ? "double" : "single")));
        WriteAssembly(game.EntryAssemblyPath, hooked: false);
        PlaceManager(game);
        var style = quote == '"' ? "double" : "single";
        WriteWindowsDoorstop(game.GameRoot, $"{quote}false{quote}", $"{quote}{target}{quote}", version: true);
        check(Read(game).Injection == InjectionMethod.None, $"Quoted {style} enabled=false stays disabled");
        WriteWindowsDoorstop(game.GameRoot, $"{quote}true{quote}", $"{quote}{target}{quote}", version: true);
        check(Read(game).Injection == InjectionMethod.Doorstop, $"Quoted {style} enabled and target_assembly count as UMM Doorstop");
        InstallerFor(root).RemoveHook(game);
        check(!File.Exists(Path.Combine(game.GameRoot, "winhttp.dll")) &&
              !File.Exists(Path.Combine(game.GameRoot, "doorstop_config.ini")) &&
              Backups(game.GameRoot, "winhttp.dll").Any() &&
              Backups(game.GameRoot, "doorstop_config.ini").Any(),
            $"Uninstall removes Doorstop files when {style} quotes surround the UMM target");
    }

    private static void CheckProtonTargets(string root, Action<bool, string> check)
    {
        var standard = WindowsGame(Path.Combine(root, "doorstop-proton-default"));
        WriteAssembly(standard.EntryAssemblyPath, hooked: false);
        PlaceManager(standard);
        var dll = Path.GetFullPath(Path.Combine(standard.ManagerPath, "UnityModManager.dll"));
        var drivePath = "Z:" + dll.Replace('/', '\\');
        WriteWindowsDoorstop(standard.GameRoot, "\"true\"", "\"" + drivePath + "\"", version: true);
        check(Read(standard).Injection == InjectionMethod.Doorstop,
            "A quoted Proton Z: target maps through Wine's default root");

        var library = Path.GetFullPath(Path.Combine(root, "proton-library"));
        var custom = WindowsGame(Path.Combine(library, "steamapps", "common", "A Dance of Fire and Ice"));
        WriteAssembly(custom.EntryAssemblyPath, hooked: false);
        PlaceManager(custom);
        var dosdevices = Path.Combine(library, "steamapps", "compatdata", "977950", "pfx", "dosdevices");
        Directory.CreateDirectory(dosdevices);
        Directory.CreateSymbolicLink(Path.Combine(dosdevices, "z:"), library);
        var relative = Path.GetRelativePath(library, Path.GetFullPath(Path.Combine(custom.ManagerPath, "UnityModManager.dll")));
        WriteWindowsDoorstop(custom.GameRoot, "true", "Z:\\" + relative.Replace(Path.DirectorySeparatorChar, '\\'), version: false);
        check(Read(custom).Injection == InjectionMethod.Doorstop,
            "A Proton Z: target resolves through the game's dosdevices mapping");
    }

    private static void WithSteamClient(string steamRoot, Action body)
    {
        const string variable = "STEAM_COMPAT_CLIENT_INSTALL_PATH";
        var previous = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, steamRoot);
        try
        {
            body();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    private static void WriteLaunchOptions(string steamRoot, string apps)
    {
        var configDir = Path.Combine(steamRoot, "userdata", "1", "config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "localconfig.vdf"),
            "\"UserLocalConfigStore\"\n{\n\"Software\"\n{\n\"Valve\"\n{\n\"Steam\"\n{\n\"apps\"\n{\n" + apps + "\n}\n}\n}\n}\n}\n");
    }

    private static InstallStatus Read(GameInstallation layout) => InstallerFor(layout.GameRoot).ReadStatus(layout);

    private static DnlibUmmInstaller InstallerFor(string root) => new(new TestLog(), new TestAppData(root));

    private static bool Readable(InstallStatus status) =>
        status.Warning is null || !status.Warning.StartsWith("Could not inspect hook", StringComparison.Ordinal);

    private static IEnumerable<string> Backups(string directory, string name) =>
        Directory.EnumerateFiles(directory, name + ".nativeumm_backup_*");

    private static bool StarterRemains(string path)
    {
        using var module = ModuleDefMD.Load(File.ReadAllBytes(path));
        return module.Types.Any(type => type.FullName == "UnityModManagerNet.Injection.UnityModManagerStarter");
    }

    private static void PlaceManager(GameInstallation layout)
    {
        Directory.CreateDirectory(layout.ManagerPath);
        File.WriteAllBytes(Path.Combine(layout.ManagerPath, "UnityModManager.dll"), [0x4d, 0x5a, 0, 0]);
    }

    private static void WriteWindowsDoorstop(string root, string enabled, string target, bool version)
    {
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "winhttp.dll"), [0x4d, 0x5a, 1, 2]);
        File.WriteAllText(Path.Combine(root, "doorstop_config.ini"),
            "[General]\r\n# Enable Doorstop?\r\nenabled = " + enabled + "\r\ntarget_assembly = " + target + "\r\n");
        var versionPath = Path.Combine(root, ".doorstop_version");
        if (version)
            File.WriteAllText(versionPath, "4.6.0");
        else if (File.Exists(versionPath))
            File.Delete(versionPath);
    }

    private static void WriteUnixDoorstop(string root, string library, string enabled, string target)
    {
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, library), [0x7f, (byte)'E', (byte)'L', (byte)'F']);
        File.WriteAllText(Path.Combine(root, "run.sh"),
            "#!/bin/sh\n# Doorstop start script\nenabled=\"" + enabled + "\"\ntarget_assembly=\"" + target + "\"\n" +
            "enabled=\"$(doorstop_bool \"$2\")\"\ntarget_assembly=\"$(abs_path \"$target_assembly\")\"\n" +
            "export DOORSTOP_ENABLED=\"$enabled\"\nexport DOORSTOP_TARGET_ASSEMBLY=\"$target_assembly\"\n");
        File.WriteAllText(Path.Combine(root, ".doorstop_version"), "4.6.0");
    }

    private static GameInstallation WindowsGame(string root)
    {
        var managed = Path.Combine(root, "A Dance of Fire and Ice_Data", "Managed");
        Directory.CreateDirectory(managed);
        var entry = Path.Combine(managed, "UnityEngine.CoreModule.dll");
        return new(root, Path.Combine(root, "A Dance of Fire and Ice.exe"), managed, entry,
            Path.Combine(managed, "UnityModManager"), Path.Combine(root, "Mods"), entry + ".original_");
    }

    private static GameInstallation LinuxGame(string root)
    {
        var managed = Path.Combine(root, "ADanceOfFireAndIce_Data", "Managed");
        Directory.CreateDirectory(managed);
        var entry = Path.Combine(managed, "UnityEngine.CoreModule.dll");
        return new(root, Path.Combine(root, "ADanceOfFireAndIce.x86_64"), managed, entry,
            Path.Combine(managed, "UnityModManager"), Path.Combine(root, "Mods"), entry + ".original_");
    }

    private static GameInstallation MacGame(string root)
    {
        var app = Path.Combine(root, "ADanceOfFireAndIce.app");
        var managed = Path.Combine(app, "Contents", "Resources", "Data", "Managed");
        Directory.CreateDirectory(managed);
        var entry = Path.Combine(managed, "UnityEngine.CoreModule.dll");
        return new(root, app, managed, entry,
            Path.Combine(managed, "UnityModManager"), Path.Combine(root, "Mods"), entry + ".original_");
    }

    private static void WriteAssembly(string path, bool hooked)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var module = new ModuleDefUser("UnityEngine.CoreModule.dll") { Kind = ModuleKind.Dll };
        var assembly = new AssemblyDefUser("UnityEngine.CoreModule", new Version(1, 0, 0, 0));
        assembly.Modules.Add(module);
        var host = new TypeDefUser("UnityEngine", "MonoBehaviour", module.CorLibTypes.Object.TypeDefOrRef)
        {
            Attributes = DnTypeAttributes.Public | DnTypeAttributes.Abstract | DnTypeAttributes.Class
        };
        var cctor = new MethodDefUser(".cctor", MethodSig.CreateStatic(module.CorLibTypes.Void),
            DnMethodImplAttributes.IL | DnMethodImplAttributes.Managed,
            DnMethodAttributes.Private | DnMethodAttributes.Static | DnMethodAttributes.HideBySig |
            DnMethodAttributes.SpecialName | DnMethodAttributes.RTSpecialName)
        { Body = new CilBody() };
        if (hooked)
        {
            var starter = new TypeDefUser("UnityModManagerNet.Injection", "UnityModManagerStarter", module.CorLibTypes.Object.TypeDefOrRef)
            {
                Attributes = DnTypeAttributes.Public | DnTypeAttributes.Class
            };
            var start = new MethodDefUser("Start", MethodSig.CreateStatic(module.CorLibTypes.Void),
                DnMethodImplAttributes.IL | DnMethodImplAttributes.Managed,
                DnMethodAttributes.Public | DnMethodAttributes.Static | DnMethodAttributes.HideBySig)
            { Body = new CilBody() };
            start.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            starter.Methods.Add(start);
            module.Types.Add(starter);
            cctor.Body.Instructions.Add(OpCodes.Call.ToInstruction(start));
        }
        cctor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        host.Methods.Add(cctor);
        module.Types.Add(host);
        module.Write(path);
    }

    private static string PayloadDirectory()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "Resources", "UMMPayload", "UnityModManager.dll");
                if (File.Exists(candidate))
                    return Path.GetDirectoryName(candidate)!;
            }
        }
        throw new Exception("Bundled UMM payload was not found.");
    }

    private sealed class TestAppData(string root) : IAppDataPaths
    {
        public string Cache => Path.Combine(root, "cache");
        public string RemovedMods => Path.Combine(root, "removed-mods");
        public string RemovedUmm => Path.Combine(root, "removed-umm");
    }

    private sealed class TestLog : IOperationLog
    {
        private readonly List<OperationLogLine> lines = [];
        public void Begin() => lines.Clear();
        public void Info(string message) => lines.Add(new("Info", message));
        public void Warn(string message) => lines.Add(new("Warn", message));
        public void Fail(string message) => lines.Add(new("Fail", message));
        public IReadOnlyList<OperationLogLine> Collect() => lines;
    }
}
