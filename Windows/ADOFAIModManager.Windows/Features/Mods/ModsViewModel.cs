using System.Collections.ObjectModel;
using ADOFAIModManager.Windows.Application.Abstractions;
using ADOFAIModManager.Windows.Models;
using ADOFAIModManager.Windows.ViewModels;

namespace ADOFAIModManager.Windows.Features.Mods;

internal sealed class ModsViewModel(AppSession session, IWorkspaceShell workspace) : ObservableObject
{
    internal Func<Task>? RefreshRequested { get; set; }
    public ObservableCollection<ModViewItem> Mods { get; } = [];
    public string? ModsDirectory => session.Installation?.ModsPath;
    public void OpenModsFolder() => workspace.OpenFolder(ModsDirectory);

    public Task RefreshAsync() => session.RunAsync("상태 확인 중…", async () =>
    {
        if (RefreshRequested is not null) await RefreshRequested();
    });

    public async Task<ModImportPreview?> InspectModAsync(string zipPath)
    {
        if (session.Installation is null)
        {
            session.SetError("먼저 얼불춤 폴더를 선택해 주세요.");
            return null;
        }
        ModImportPreview? preview = null;
        await session.RunAsync("모드 확인 중…", async () =>
        {
            var inspection = await Task.Run(() => session.GameService.InspectMod(session.Installation, zipPath));
            preview = new ModImportPreview(zipPath, inspection.Id, inspection.DisplayName,
                inspection.Version, inspection.AlreadyInstalled);
        });
        return preview;
    }

    public async Task InstallModAsync(ModImportPreview preview)
    {
        await session.RunGameActionAsync("모드 추가 중…", layout =>
        {
            session.GameService.InstallMod(layout, preview.ZipPath);
            return Task.CompletedTask;
        });
        if (!session.HasError && RefreshRequested is not null) await RefreshRequested();
    }

    public async Task SetModEnabledAsync(ModViewItem mod, bool enabled)
    {
        var old = mod.Enabled;
        mod.Enabled = enabled;
        await session.RunGameActionAsync(enabled ? "모드 켜는 중…" : "모드 끄는 중…", layout =>
        {
            session.GameService.SetModEnabled(layout, mod.Id, enabled);
            return Task.CompletedTask;
        });
        if (session.HasError) mod.Enabled = old;
        else if (RefreshRequested is not null) await RefreshRequested();
    }

    public async Task PermanentlyRemoveModAsync(ModViewItem mod)
    {
        await session.RunGameActionAsync("모드 삭제 중…", layout =>
        {
            session.GameService.PermanentlyRemoveMod(layout, mod.Path);
            return Task.CompletedTask;
        });
        if (!session.HasError && RefreshRequested is not null) await RefreshRequested();
    }

    internal async Task RefreshCoreAsync()
    {
        if (session.Installation is not { } layout)
        {
            Mods.Clear();
            RaisePropertyChanged(nameof(ModsDirectory));
            return;
        }

        var mods = await Task.Run(() => session.GameService.ReadMods(layout));
        var updated = mods.Select(mod => new ModViewItem
        {
            Id = mod.Id,
            Name = mod.DisplayName,
            Version = mod.Version,
            Path = mod.Path,
            Status = mod.Status,
            Installed = mod.Installed,
            Enabled = mod.Enabled,
            HomePage = mod.HomePage,
            RequirementSummary = string.Join(", ", mod.Requirements
                .Where(item => item.State != "OK")
                .Select(item => $"{item.Id}: {item.State}"))
        }).ToList();
        SynchronizeMods(updated);
        RaisePropertyChanged(nameof(ModsDirectory));
    }

    private void SynchronizeMods(IReadOnlyList<ModViewItem> updated)
    {
        for (var targetIndex = 0; targetIndex < updated.Count; targetIndex++)
        {
            var incoming = updated[targetIndex];
            var existingIndex = -1;
            for (var index = targetIndex; index < Mods.Count; index++)
            {
                if (!Mods[index].Path.Equals(incoming.Path, StringComparison.OrdinalIgnoreCase)) continue;
                existingIndex = index;
                break;
            }
            if (existingIndex < 0) Mods.Insert(targetIndex, incoming);
            else
            {
                if (existingIndex != targetIndex) Mods.Move(existingIndex, targetIndex);
                Mods[targetIndex].UpdateFrom(incoming);
            }
        }
        while (Mods.Count > updated.Count) Mods.RemoveAt(Mods.Count - 1);
    }
}
