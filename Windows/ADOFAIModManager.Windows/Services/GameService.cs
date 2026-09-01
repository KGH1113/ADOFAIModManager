using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;
using NativeUmm.Domain.Installation;
using NativeUmm.Domain.Mods;

namespace ADOFAIModManager.Windows.Services;

internal sealed class GameService(
    IGameLocator gameLocator,
    IGameProcessProbe gameProcessProbe,
    IUmmInstaller installer,
    IModService modService,
    IOperationLog operationLog,
    IAppDataPaths appData)
{
    public GameInstallation? Detect(string? selectedPath) => gameLocator.Locate(selectedPath);

    private void EnsureGameClosed()
    {
        if (gameProcessProbe.IsRunning())
            throw new GameRunningException();
    }

    public async Task InstallAsync(GameInstallation layout, bool repair)
    {
        EnsureGameClosed();
        EnsureWritable(layout);
        operationLog.Begin();
        await installer.InstallAsync(layout, payloadDirectory: null, forcePayload: repair);
    }

    public void RemoveUmm(GameInstallation layout)
    {
        EnsureGameClosed();
        EnsureWritable(layout);
        operationLog.Begin();
        installer.RemoveHook(layout);

        if (!Directory.Exists(layout.ManagerPath))
            return;
        var destination = Path.Combine(appData.RemovedUmm, $"UnityModManager_{DateTime.Now:yyyyMMdd_HHmmss}");
        MoveDirectory(layout.ManagerPath, destination);
        operationLog.Info("UnityModManager files were moved to a recoverable backup.");
    }

    public void RestoreOriginal(GameInstallation layout)
    {
        EnsureGameClosed();
        EnsureWritable(layout);
        operationLog.Begin();
        installer.RestoreOriginal(layout);
    }

    public InstallStatus ReadStatus(GameInstallation layout) => installer.ReadStatus(layout);
    public List<ModInfo> ReadMods(GameInstallation layout) => modService.List(layout);
    public ModInspection InspectMod(GameInstallation layout, string zipPath) => modService.Inspect(layout, zipPath);

    public void InstallMod(GameInstallation layout, string zipPath)
    {
        EnsureGameClosed();
        operationLog.Begin();
        modService.Install(layout, zipPath);
    }

    public void SetModEnabled(GameInstallation layout, string modId, bool enabled)
    {
        EnsureGameClosed();
        operationLog.Begin();
        modService.SetEnabled(layout, modId, enabled);
    }

    public void PermanentlyRemoveMod(GameInstallation layout, string path)
    {
        EnsureGameClosed();
        operationLog.Begin();
        modService.Remove(layout, path);
    }

    public IReadOnlyList<OperationLogLine> CollectOperationLog() => operationLog.Collect();

    private static void EnsureWritable(GameInstallation layout)
    {
        var probe = Path.Combine(layout.ManagedPath, $".adofai-write-test-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probe, "ok");
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException("Game folder is not writable.", ex);
        }
        finally
        {
            try { File.Delete(probe); } catch { }
        }
    }

    private static void MoveDirectory(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        try
        {
            Directory.Move(source, destination);
        }
        catch (IOException)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(destination, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
            Directory.Delete(source, recursive: true);
        }
    }
}

internal sealed class GameRunningException : InvalidOperationException { }
