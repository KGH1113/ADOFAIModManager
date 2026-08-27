namespace NativeUmm.Domain.Mods;

public sealed record ModRequirement(string Id, string? Version, string State);

public sealed record ModInfo(
    string Id,
    string DisplayName,
    string Version,
    string? ManagerVersion,
    string? HomePage,
    string Status,
    string Path,
    bool Installed,
    bool Enabled,
    IReadOnlyList<ModRequirement> Requirements);

public sealed record ModInspection(
    string Id,
    string DisplayName,
    string Version,
    bool AlreadyInstalled);
