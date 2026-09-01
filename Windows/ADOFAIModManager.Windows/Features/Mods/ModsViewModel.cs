using System.Collections.ObjectModel;
using ADOFAIModManager.Windows.Application.Abstractions;
using ADOFAIModManager.Windows.Application.Localization;
using ADOFAIModManager.Windows.Models;
using ADOFAIModManager.Windows.ViewModels;
using NativeUmm.Domain.Mods;

namespace ADOFAIModManager.Windows.Features.Mods;

internal sealed class ModsViewModel : ObservableObject
{
    private readonly AppSession session;
    private readonly IWorkspaceShell workspace;

    public ModsViewModel(AppSession session, IWorkspaceShell workspace)
    {
        this.session = session;
        this.workspace = workspace;
        Localization = session.Localization;
        Localization.LanguageChanged += (_, _) =>
        {
            foreach (var mod in Mods) mod.RefreshLocalizedText();
        };
    }

    internal Func<Task>? RefreshRequested { get; set; }
    public ILocalizationService Localization { get; }
    public ObservableCollection<ModViewItem> Mods { get; } = [];
    public string? ModsDirectory => session.Installation?.ModsPath;
    public void OpenModsFolder() => workspace.OpenFolder(ModsDirectory);

    public Task RefreshAsync() => session.RunAsync("Activity_CheckStatus", async () =>
    {
        if (RefreshRequested is not null) await RefreshRequested();
    });

    public async Task<ModImportPreview?> InspectModAsync(string zipPath)
    {
        if (session.Installation is null)
        {
            session.SetLocalizedError("Error_SelectGameFirst");
            return null;
        }
        ModImportPreview? preview = null;
        await session.RunAsync("Activity_InspectMod", async () =>
        {
            var inspection = await Task.Run(() => session.GameService.InspectMod(session.Installation, zipPath));
            preview = new ModImportPreview(zipPath, inspection.Id, inspection.DisplayName,
                inspection.Version, inspection.AlreadyInstalled);
        });
        return preview;
    }

    public async Task InstallModAsync(ModImportPreview preview)
    {
        await session.RunGameActionAsync("Activity_InstallMod", layout =>
        {
            session.GameService.InstallMod(layout, preview.ZipPath);
            return Task.CompletedTask;
        });
        if (!session.HasError && RefreshRequested is not null) await RefreshRequested();
    }

    public async Task SetModEnabledAsync(ModViewItem mod, bool enabled)
    {
        if (session.IsBusy) return;
        var old = mod.Enabled;
        mod.Enabled = enabled;
        await session.RunGameActionAsync(enabled ? "Activity_EnableMod" : "Activity_DisableMod", layout =>
        {
            session.GameService.SetModEnabled(layout, mod.Id, enabled);
            return Task.CompletedTask;
        });
        if (session.HasError)
        {
            mod.Enabled = old;
            return;
        }
        await RefreshMetadataInPlaceAsync();
    }

    public async Task PermanentlyRemoveModAsync(ModViewItem mod)
    {
        if (session.IsBusy) return;
        await session.RunGameActionAsync("Activity_DeleteMod", layout =>
        {
            session.GameService.PermanentlyRemoveMod(layout, mod.Path);
            return Task.CompletedTask;
        });
        if (session.HasError) return;
        Mods.Remove(mod);
        await RefreshMetadataInPlaceAsync();
    }

    internal async Task RefreshCoreAsync()
    {
        if (session.Installation is not { } layout)
        {
            Mods.Clear();
            RaisePropertyChanged(nameof(ModsDirectory));
            return;
        }

        var updated = (await Task.Run(() => session.GameService.ReadMods(layout)))
            .Select(BuildItem)
            .ToList();
        SynchronizeMods(updated);
        RaisePropertyChanged(nameof(ModsDirectory));
    }

    private async Task RefreshMetadataInPlaceAsync()
    {
        if (session.Installation is not { } layout) return;
        try
        {
            var updated = (await Task.Run(() => session.GameService.ReadMods(layout)))
                .ToDictionary(mod => mod.Path, StringComparer.OrdinalIgnoreCase);
            foreach (var existing in Mods)
                if (updated.TryGetValue(existing.Path, out var incoming))
                    existing.UpdateFrom(BuildItem(incoming));
        }
        catch (Exception exception)
        {
            session.SetError(exception.Message);
        }
    }

    private ModViewItem BuildItem(ModInfo mod)
    {
        var item = new ModViewItem
        {
            Localization = Localization,
            Id = mod.Id,
            Name = mod.DisplayName,
            Version = mod.Version,
            Path = mod.Path,
            Status = mod.Status,
            Enabled = mod.Enabled,
            HomePage = mod.HomePage
        };
        item.SetRequirements(mod.Requirements
            .Where(requirement => requirement.State != "OK")
            .Select(requirement => (requirement.Id, requirement.State))
            .ToList());
        return item;
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
