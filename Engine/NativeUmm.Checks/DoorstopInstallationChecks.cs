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
        check(Read(mac).Injection == InjectionMethod.None && Read(mac).Warning is not null,
            "A leftover macOS desktop launcher is not evidence that the app starts through Doorstop");
        File.Delete(Path.Combine(mac.GameRoot, "libdoorstop.dylib"));
        check(Read(mac).Injection == InjectionMethod.None, "macOS Doorstop without libdoorstop.dylib is not injecting");

        var repair = WindowsGame(Path.Combine(root, "doorstop-repair"));
        WriteAssembly(repair.EntryAssemblyPath, hooked: false);
        var original = File.ReadAllBytes(repair.EntryAssemblyPath);
        WriteWindowsDoorstop(repair.GameRoot, "true", ummTarget, version: true);
        var log = new TestLog();
        InstallerFor(root, log).InstallAsync(repair, PayloadDirectory(), forcePayload: false)
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
        InstallerFor(root).InstallAsync(again, PayloadDirectory(), forcePayload: true)
            .GetAwaiter().GetResult();
        check(File.ReadAllBytes(again.EntryAssemblyPath).AsSpan().SequenceEqual(patched) &&
              Read(again).Injection == InjectionMethod.Both && Read(again).HookInstalled,
            "Install does not rewrite an assembly patch that is already beside an active Doorstop");

        var alongside = WindowsGame(Path.Combine(root, "doorstop-bepinex-install"));
        WriteAssembly(alongside.EntryAssemblyPath, hooked: false);
        WriteWindowsDoorstop(alongside.GameRoot, "true", @"BepInEx\core\BepInEx.Preloader.dll", version: true);
        var foreign = File.ReadAllBytes(Path.Combine(alongside.GameRoot, "winhttp.dll"));
        InstallerFor(root).InstallAsync(alongside, PayloadDirectory(), forcePayload: false)
            .GetAwaiter().GetResult();
        check(Read(alongside).Injection == InjectionMethod.Assembly &&
              File.ReadAllBytes(Path.Combine(alongside.GameRoot, "winhttp.dll")).AsSpan().SequenceEqual(foreign),
            "Install still patches the assembly when Doorstop points at BepInEx and leaves those files alone");

        var disabled = WindowsGame(Path.Combine(root, "doorstop-disabled-install"));
        WriteAssembly(disabled.EntryAssemblyPath, hooked: false);
        WriteWindowsDoorstop(disabled.GameRoot, "false", ummTarget, version: true);
        InstallerFor(root).InstallAsync(disabled, PayloadDirectory(), forcePayload: false)
            .GetAwaiter().GetResult();
        check(Read(disabled).Injection == InjectionMethod.Assembly &&
              File.Exists(Path.Combine(disabled.GameRoot, "winhttp.dll")) &&
              File.ReadAllText(Path.Combine(disabled.GameRoot, "doorstop_config.ini")).Contains("enabled = false", StringComparison.Ordinal),
            "Install patches the assembly when UMM Doorstop is disabled");

        CheckUnixLaunches(root, check);

        var wrapped = LinuxGame(Path.Combine(root, "doorstop-linux-wrapper"));
        WriteAssembly(wrapped.EntryAssemblyPath, hooked: false);
        PlaceManager(wrapped);
        WriteUnixDoorstop(wrapped.GameRoot, "libdoorstop.so", "1", "ADanceOfFireAndIce_Data/Managed/UnityModManager/UnityModManager.dll");
        File.WriteAllText(wrapped.AppPath, "#!/bin/sh\nexec ./run.sh \"$@\"\n");
        check(Read(wrapped).Injection == InjectionMethod.Doorstop,
            "The selected game executable can launch its own configured Doorstop script");

        var preloaded = LinuxGame(Path.Combine(root, "doorstop-linux-preload"));
        PrepareUnixGame(preloaded);
        WritePreloadWrapper(preloaded, enabled: null, target: null);
        CheckUncertainInstall(InstallerFor(root), preloaded, check,
            "Preloading libdoorstop without verified enabled and target exports blocks a second injector");
        WritePreloadWrapper(preloaded, "1", linuxTarget);
        check(Read(preloaded).Injection == InjectionMethod.Doorstop,
            "A selected executable with complete literal Doorstop environment starts UMM");
        WritePreloadWrapper(preloaded, "0", linuxTarget);
        check(Read(preloaded).Injection == InjectionMethod.None,
            "A disabled executable preload does not activate the UMM target from a separate run.sh");
        WritePreloadWrapper(preloaded, "1", "BepInEx/core/BepInEx.Preloader.dll");
        check(Read(preloaded).Injection == InjectionMethod.None,
            "A preload wrapper targeting BepInEx does not activate UMM's separate run.sh config");
        WritePreloadWrapper(preloaded, "1", linuxTarget);
        File.WriteAllText(preloaded.AppPath, File.ReadAllText(preloaded.AppPath).Replace(
            "export DOORSTOP_ENABLED=\"1\"\n",
            "export DOORSTOP_ENABLED=\"1\"; export DOORSTOP_ENABLED=\"0\"\n", StringComparison.Ordinal));
        CheckUncertainInstall(InstallerFor(root), preloaded, check,
            "A semicolon assignment suffix is not mistaken for a verified enabled preload");
    }

    private static void CheckUnixLaunches(string root, Action<bool, string> check)
    {
        var previousClient = Environment.GetEnvironmentVariable("STEAM_COMPAT_CLIENT_INSTALL_PATH");
        var client = Path.Combine(root, "steam client fixture");
        var game = LinuxGame(Path.Combine(client, "steamapps", "common", "ADanceOfFireAndIce"));
        PrepareUnixGame(game);
        WriteSteamLibraries(client, client);
        WriteSteamManifest(client, game);
        SelectSteamUser(client, 1);
        var installer = InstallerFor(root, steamClientRoots: [client]);

        WriteLaunchOptions(client, "\"977950\" { \"LastPlayed\" \"123\" }");
        File.WriteAllText(Path.Combine(game.GameRoot, "ADanceOfFireAndIce.desktop"),
            "[Desktop Entry]\nType=Application\nExec=\"" + Path.Combine(game.GameRoot, "run.sh") + "\"\n");
        var missingOptions = installer.ReadStatus(game);
        check(missingOptions.Injection == InjectionMethod.None &&
              !DoorstopProbe.Inspect(game, [client]).NeedsLaunchConfirmation,
            "Missing Steam LaunchOptions use the game binary despite a leftover desktop launcher");
        var directBefore = File.ReadAllBytes(game.EntryAssemblyPath);
        installer.InstallAsync(game, PayloadDirectory(), forcePayload: false).GetAwaiter().GetResult();
        check(installer.ReadStatus(game).Injection == InjectionMethod.Assembly &&
              !File.ReadAllBytes(game.EntryAssemblyPath).AsSpan().SequenceEqual(directBefore),
            "Install patches the assembly when default Steam launch options bypass a leftover desktop file");
        WriteAssembly(game.EntryAssemblyPath, hooked: false);

        SetLaunchOptions(client, "");
        check(installer.ReadStatus(game).Injection == InjectionMethod.None,
            "Empty Steam LaunchOptions also use the game binary");
        WriteLaunchOptions(client, "\"123\" { \"LaunchOptions\" \"./run.sh %command%\" }");
        check(installer.ReadStatus(game).Injection == InjectionMethod.None,
            "Another game's launch options do not change ADOFAI's default direct launch");

        SetLaunchOptions(client, "./run.sh %command%");
        check(installer.ReadStatus(game).Injection == InjectionMethod.Doorstop,
            "A manifest-backed Steam game uses its own relative run.sh launch option");
        var launchedBefore = File.ReadAllBytes(game.EntryAssemblyPath);
        installer.InstallAsync(game, PayloadDirectory(), forcePayload: false).GetAwaiter().GetResult();
        check(installer.ReadStatus(game).Injection == InjectionMethod.Doorstop &&
              !installer.ReadStatus(game).HookInstalled &&
              File.ReadAllBytes(game.EntryAssemblyPath).AsSpan().SequenceEqual(launchedBefore),
            "Install preserves the assembly when Steam starts this game's Doorstop script");
        SetLaunchOptions(client, "bash \"" + Path.Combine(game.GameRoot, "run.sh") + "\" %command%");
        check(installer.ReadStatus(game).Injection == InjectionMethod.Doorstop,
            "A shell wrapper can launch the game's quoted absolute run.sh path containing spaces");

        SetLaunchOptions(client, "./run.sh --doorstop-enabled false %command%");
        CheckUncertainInstall(installer, game, check,
            "A run.sh enabled override is not mistaken for its configured active UMM launch");
        SetLaunchOptions(client,
            "./run.sh --doorstop_target_assembly BepInEx/core/BepInEx.Preloader.dll %command%");
        CheckUncertainInstall(installer, game, check,
            "A legacy run.sh target override does not inherit the file's UMM target");
        WriteUnixDoorstop(game.GameRoot, "libdoorstop.so", "0",
            "ADanceOfFireAndIce_Data/Managed/UnityModManager/UnityModManager.dll");
        SetLaunchOptions(client, "./run.sh --doorstop-enabled true %command%");
        CheckUncertainInstall(installer, game, check,
            "A launch-time enabled override blocks assembly installation even when run.sh defaults to disabled");
        WriteUnixDoorstop(game.GameRoot, "libdoorstop.so", "1",
            "ADanceOfFireAndIce_Data/Managed/UnityModManager/UnityModManager.dll");
        SetLaunchOptions(client, "./run.sh %command%");

        SetLaunchOptions(client, "\"" + Path.Combine(root, "another-game", "run.sh") + "\" %command%");
        CheckUncertainInstall(installer, game, check,
            "An unrelated absolute run.sh does not prove this game's launch path or permit a second injector");
        SetLaunchOptions(client, "gamemoderun %command% --note=run.sh");
        CheckUncertainInstall(installer, game, check,
            "A run.sh substring in an unknown wrapper does not activate Doorstop or permit assembly installation");

        SetLaunchOptions(client, "%command%", account: 1);
        SetLaunchOptions(client, "./run.sh %command%", account: 2);
        SelectSteamUser(client, 1, 2);
        check(installer.ReadStatus(game).Injection == InjectionMethod.None,
            "The current Steam account's direct launch wins over a previous account's Doorstop setting");
        SetLaunchOptions(client, "./run.sh %command%", account: 1);
        SetLaunchOptions(client, "%command%", account: 2);
        check(installer.ReadStatus(game).Injection == InjectionMethod.Doorstop,
            "The current Steam account's Doorstop launch is not hidden by a previous direct setting");
        File.Delete(Path.Combine(client, "config", "loginusers.vdf"));
        CheckUncertainInstall(installer, game, check,
            "Multiple Steam accounts without a selected current account do not guess a launch method");
        SelectSteamUser(client, 1, 2);

        var secondaryLibrary = Path.Combine(root, "registered secondary library");
        var secondary = LinuxGame(Path.Combine(secondaryLibrary, "steamapps", "common", "ADanceOfFireAndIce"));
        PrepareUnixGame(secondary);
        WriteSteamManifest(secondaryLibrary, secondary);
        WriteSteamLibraries(client, client, secondaryLibrary);
        SetLaunchOptions(secondaryLibrary, "%command%");
        check(installer.ReadStatus(secondary).Injection == InjectionMethod.Doorstop,
            "A registered secondary library reads the current account's options from the primary Steam client");
        var secondaryBefore = File.ReadAllBytes(secondary.EntryAssemblyPath);
        installer.InstallAsync(secondary, PayloadDirectory(), forcePayload: true).GetAwaiter().GetResult();
        check(!installer.ReadStatus(secondary).HookInstalled &&
              File.ReadAllBytes(secondary.EntryAssemblyPath).AsSpan().SequenceEqual(secondaryBefore),
            "Repair of a registered secondary library does not add a second injector");

        var unregisteredLibrary = Path.Combine(root, "unregistered library");
        var unregistered = LinuxGame(Path.Combine(unregisteredLibrary, "steamapps", "common", "ADanceOfFireAndIce"));
        PrepareUnixGame(unregistered);
        WriteSteamManifest(unregisteredLibrary, unregistered);
        CheckUncertainInstall(installer, unregistered, check,
            "A manifest in an unregistered library does not inherit another installation's Steam options");

        var separate = LinuxGame(Path.Combine(root, "manually selected game"));
        PrepareUnixGame(separate);
        File.WriteAllText(Path.Combine(separate.GameRoot, "unused.desktop"),
            "[Desktop Entry]\nType=Application\nExec=\"" + Path.Combine(separate.GameRoot, "run.sh") + "\"\n");
        CheckUncertainInstall(installer, separate, check,
            "A separate game copy does not inherit the Steam client's active Doorstop setting");
        SetLaunchOptions(client, "%command%");
        CheckUncertainInstall(installer, separate, check,
            "A separate game copy does not inherit the Steam client's direct launch setting");
        var withoutClients = InstallerFor(root);
        CheckUncertainInstall(withoutClients, game, check,
            "Explicitly empty Steam roots ignore host settings and require confirmation of an enabled Unix loader");
        withoutClients.RemoveHook(separate);
        check(!File.Exists(Path.Combine(separate.GameRoot, "run.sh")) &&
              !File.Exists(Path.Combine(separate.GameRoot, "libdoorstop.so")) &&
              Backups(separate.GameRoot, "run.sh").Any(),
            "An uncertain UMM Doorstop deployment can still be safely removed");

        var disabled = LinuxGame(Path.Combine(root, "disabled Unix Doorstop"));
        PrepareUnixGame(disabled);
        WriteUnixDoorstop(disabled.GameRoot, "libdoorstop.so", "0",
            "ADanceOfFireAndIce_Data/Managed/UnityModManager/UnityModManager.dll");
        withoutClients.InstallAsync(disabled, PayloadDirectory(), forcePayload: false).GetAwaiter().GetResult();
        check(withoutClients.ReadStatus(disabled).Injection == InjectionMethod.Assembly,
            "A disabled Unix Doorstop does not require launch confirmation before assembly installation");

        var mac = MacGame(Path.Combine(client, "steamapps", "common", "ADOFAI mac with spaces"));
        WriteAssembly(mac.EntryAssemblyPath, hooked: false);
        PlaceManager(mac);
        WriteUnixDoorstop(mac.GameRoot, "libdoorstop.dylib", "1",
            "ADanceOfFireAndIce.app/Contents/Resources/Data/Managed/UnityModManager/UnityModManager.dll");
        WriteSteamManifest(client, mac);
        SetLaunchOptions(client, "bash \"" + Path.Combine(mac.GameRoot, "run.sh") + "\" %command%");
        check(installer.ReadStatus(mac).Injection == InjectionMethod.Doorstop,
            "macOS Doorstop handles a quoted Steam launcher and dylib path containing spaces");
        check(Environment.GetEnvironmentVariable("STEAM_COMPAT_CLIENT_INSTALL_PATH") == previousClient,
            "Doorstop checks leave the host's Steam environment unchanged");
    }

    private static void CheckUncertainInstall(DnlibUmmInstaller installer, GameInstallation game,
        Action<bool, string> check, string description)
    {
        var beforeCore = File.ReadAllBytes(game.EntryAssemblyPath);
        var manager = Path.Combine(game.ManagerPath, "UnityModManager.dll");
        var beforeManager = File.ReadAllBytes(manager);
        var config = Path.Combine(game.ManagerPath, "Config.xml");
        var hadConfig = File.Exists(config);
        var beforeConfig = hadConfig ? File.ReadAllBytes(config) : [];
        var hadBackup = File.Exists(game.OriginalBackupPath);
        var beforeBackup = hadBackup ? File.ReadAllBytes(game.OriginalBackupPath) : [];
        var rejected = false;
        try
        {
            installer.InstallAsync(game, PayloadDirectory(), forcePayload: true).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException error) when (error.Message == DoorstopProbe.UnverifiedLaunchWarning)
        {
            rejected = true;
        }
        var status = installer.ReadStatus(game);
        check(status.Injection == InjectionMethod.None &&
              status.Warning?.Contains(DoorstopProbe.UnverifiedLaunchWarning, StringComparison.Ordinal) == true && rejected &&
              File.ReadAllBytes(game.EntryAssemblyPath).AsSpan().SequenceEqual(beforeCore) &&
              File.ReadAllBytes(manager).AsSpan().SequenceEqual(beforeManager) &&
              File.Exists(config) == hadConfig && (!hadConfig || File.ReadAllBytes(config).AsSpan().SequenceEqual(beforeConfig)) &&
              File.Exists(game.OriginalBackupPath) == hadBackup &&
              (!hadBackup || File.ReadAllBytes(game.OriginalBackupPath).AsSpan().SequenceEqual(beforeBackup)), description);
    }

    private static void PrepareUnixGame(GameInstallation game)
    {
        WriteAssembly(game.EntryAssemblyPath, hooked: false);
        WriteNativeGame(game.AppPath);
        PlaceManager(game);
        WriteUnixDoorstop(game.GameRoot, "libdoorstop.so", "1",
            "ADanceOfFireAndIce_Data/Managed/UnityModManager/UnityModManager.dll");
    }

    private static void WritePreloadWrapper(GameInstallation game, string? enabled, string? target)
    {
        WriteNativeGame(Path.Combine(game.GameRoot, "ADanceOfFireAndIce.real"));
        var text = "#!/bin/sh\nexport LD_PRELOAD=\"./libdoorstop.so\"\n";
        if (enabled is not null)
            text += "export DOORSTOP_ENABLED=\"" + enabled + "\"\n";
        if (target is not null)
            text += "export DOORSTOP_TARGET_ASSEMBLY=\"" + target + "\"\n";
        File.WriteAllText(game.AppPath, text + "exec ./ADanceOfFireAndIce.real \"$@\"\n");
    }

    private static void WriteNativeGame(string path) =>
        File.WriteAllBytes(path, [0x7f, (byte)'E', (byte)'L', (byte)'F']);

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

    private static void WriteSteamLibraries(string steamRoot, params string[] libraries)
    {
        var steamapps = Path.Combine(steamRoot, "steamapps");
        Directory.CreateDirectory(steamapps);
        var entries = libraries.Select((path, index) => $"\"{index}\" {{ \"path\" {VdfString(path)} }}");
        File.WriteAllText(Path.Combine(steamapps, "libraryfolders.vdf"),
            "\"libraryfolders\" { " + string.Join("\n", entries) + " }");
    }

    private static void WriteSteamManifest(string library, GameInstallation game)
    {
        var steamapps = Path.Combine(library, "steamapps");
        Directory.CreateDirectory(steamapps);
        File.WriteAllText(Path.Combine(steamapps, "appmanifest_977950.acf"),
            "\"AppState\" { \"appid\" \"977950\" \"installdir\" " + VdfString(Path.GetFileName(game.GameRoot)) + " }");
    }

    private static void SelectSteamUser(string steamRoot, uint current, params uint[] previous)
    {
        var config = Path.Combine(steamRoot, "config");
        Directory.CreateDirectory(config);
        var users = previous.Prepend(current).Select(account =>
            $"\"{76561197960265728UL + account}\" {{ \"MostRecent\" \"{(account == current ? 1 : 0)}\" }}");
        File.WriteAllText(Path.Combine(config, "loginusers.vdf"), "\"users\" { " + string.Join("\n", users) + " }");
    }

    private static void SetLaunchOptions(string steamRoot, string options, uint account = 1) =>
        WriteLaunchOptions(steamRoot, "\"977950\" { \"LaunchOptions\" " + VdfString(options) + " }", account);

    private static string VdfString(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static void WriteLaunchOptions(string steamRoot, string apps, uint account = 1)
    {
        var configDir = Path.Combine(steamRoot, "userdata", account.ToString(), "config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "localconfig.vdf"),
            "\"UserLocalConfigStore\"\n{\n\"Software\"\n{\n\"Valve\"\n{\n\"Steam\"\n{\n\"apps\"\n{\n" + apps + "\n}\n}\n}\n}\n}\n");
    }

    private static InstallStatus Read(GameInstallation layout) => InstallerFor(layout.GameRoot).ReadStatus(layout);

    private static DnlibUmmInstaller InstallerFor(string root, TestLog? log = null,
        IReadOnlyList<string>? steamClientRoots = null) => new(log ?? new TestLog(), new TestAppData(root), steamClientRoots ?? []);

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
