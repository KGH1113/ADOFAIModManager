using NativeUmm.Application.Abstractions;
using NativeUmm.Domain.Games;
using NativeUmm.Domain.Mods;
using NativeUmm.Infrastructure.Presets;

namespace NativeUmm.Infrastructure.Mods;

public sealed class FileModService(IOperationLog log, IAppDataPaths appData) : IModService
{
    public List<ModInfo> List(GameInstallation installation) => ModManager.List(installation);
    public ModInspection Inspect(GameInstallation installation, string zipPath) => ModManager.Inspect(installation, zipPath);
    public void Install(GameInstallation installation, string zipPath)
    {
        var inspection = Inspect(installation, zipPath);
        ModManager.Install(installation, zipPath, log, appData);
        Configure(installation, inspection.Id, !inspection.AlreadyInstalled);
    }
    public async Task InstallFromUrlAsync(GameInstallation installation, string url)
    {
        var existing = List(installation).Where(m => m.Installed).Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        await ModManager.InstallFromUrlAsync(installation, url, log, appData);
        foreach (var mod in List(installation).Where(m => m.Installed && !existing.Contains(m.Id)))
            Configure(installation, mod.Id, true);
    }
    public void SetEnabled(GameInstallation installation, string modId, bool enabled)
    {
        ModManager.SetEnabled(installation, modId, enabled, log);
        if (enabled) Configure(installation, modId, false);
    }
    public void Uninstall(GameInstallation installation, string path) => ModManager.Uninstall(installation, path, log, appData);
    public void Restore(GameInstallation installation, string path) => ModManager.Restore(installation, path, log, appData);
    public void Remove(GameInstallation installation, string path) => ModManager.Remove(installation, path, log, appData);

    private void Configure(GameInstallation installation, string id, bool fresh)
    {
        try
        {
            var setup = new OnboardingSetupService();
            var mods = List(installation);
            if (fresh && OnboardingSetupService.DefaultKind(id) is { } kind) setup.Apply(mods, kind);
            else setup.Reconcile(mods, id);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.Text.Json.JsonException)
        {
            // Installation succeeded. Report the separate settings failure and allow onboarding to retry it.
            log.Warn($"{id} was installed, but recommended settings could not be applied: {ex.Message}");
        }
    }
}
