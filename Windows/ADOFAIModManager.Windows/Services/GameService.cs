using System.Diagnostics;
using System.Text;
using NativeUmm;

namespace ADOFAIModManager.Windows.Services;

internal sealed class GameService
{
    public GameLayout? Detect(string? selectedPath) => GameLayout.Detect(selectedPath);

    public static bool IsGameRunning() =>
        Process.GetProcessesByName("A Dance of Fire and Ice").Length > 0
        || Process.GetProcessesByName("ADanceOfFireAndIce").Length > 0;

    public static void EnsureGameClosed()
    {
        if (IsGameRunning())
            throw new InvalidOperationException("얼불춤이 실행 중입니다. 게임을 종료한 다음 다시 시도해 주세요.");
    }

    public async Task InstallAsync(GameLayout layout, bool repair)
    {
        EnsureGameClosed();
        EnsureWritable(layout);
        Log.Begin();
        await new Installer(layout).InstallAsync(payloadDir: null, forcePayload: repair);
    }

    public void RemoveUmm(GameLayout layout)
    {
        EnsureGameClosed();
        EnsureWritable(layout);
        Log.Begin();
        var installer = new Installer(layout);
        installer.RemoveHook();

        if (!Directory.Exists(layout.ManagerPath))
            return;
        var destination = Path.Combine(AppData.RemovedUmm, $"UnityModManager_{DateTime.Now:yyyyMMdd_HHmmss}");
        MoveDirectory(layout.ManagerPath, destination);
        Log.Info("UnityModManager files were moved to a recoverable backup.");
    }

    public void RestoreOriginal(GameLayout layout)
    {
        EnsureGameClosed();
        EnsureWritable(layout);
        Log.Begin();
        new Installer(layout).RestoreOriginal();
    }

    public InstallStatus ReadStatus(GameLayout layout) => new Installer(layout).ReadStatus();
    public List<ModInfo> ReadMods(GameLayout layout) => ModManager.List(layout);
    public ModInspection InspectMod(GameLayout layout, string zipPath) => ModManager.Inspect(layout, zipPath);

    public void InstallMod(GameLayout layout, string zipPath)
    {
        EnsureGameClosed();
        Log.Begin();
        ModManager.Install(layout, zipPath);
    }

    public void SetModEnabled(GameLayout layout, string modId, bool enabled)
    {
        EnsureGameClosed();
        Log.Begin();
        ModManager.SetEnabled(layout, modId, enabled);
    }

    public void UninstallMod(GameLayout layout, string path)
    {
        EnsureGameClosed();
        Log.Begin();
        ModManager.Uninstall(layout, path);
    }

    public void RestoreMod(GameLayout layout, string path)
    {
        EnsureGameClosed();
        Log.Begin();
        ModManager.Restore(layout, path);
    }

    public void PermanentlyRemoveMod(GameLayout layout, string path)
    {
        EnsureGameClosed();
        Log.Begin();
        ModManager.Remove(layout, path);
    }

    public static IReadOnlyList<LogLine> CollectOperationLog() => Log.Collect();

    public static string ReadGameLog() => ReadTail(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "7th Beat Games", "A Dance of Fire and Ice", "Player.log"),
        30);

    public static string ReadUmmLog(GameLayout? layout) =>
        layout is null ? "" : ReadTail(Path.Combine(layout.ManagerPath, "Log.txt"), 400);

    private static string ReadTail(string path, int lineCount)
    {
        if (!File.Exists(path))
            return "";
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var queue = new Queue<string>(lineCount);
            while (reader.ReadLine() is { } line)
            {
                if (queue.Count == lineCount)
                    queue.Dequeue();
                queue.Enqueue(line);
            }
            return string.Join(Environment.NewLine, queue);
        }
        catch (Exception ex)
        {
            return $"로그를 읽을 수 없습니다: {ex.Message}";
        }
    }

    private static void EnsureWritable(GameLayout layout)
    {
        var probe = Path.Combine(layout.ManagedPath, $".adofai-write-test-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probe, "ok");
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException(
                "얼불춤 폴더를 변경할 수 없습니다. Steam의 게임 폴더 권한을 확인해 주세요.", ex);
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
