using System.ComponentModel;
using System.Runtime.CompilerServices;
using ADOFAIModManager.Windows.Application.Localization;
using Microsoft.UI.Xaml;

namespace ADOFAIModManager.Windows.Models;

internal abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void RaisePropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class ModViewItem : ObservableObject
{
    private bool _enabled;
    private string _name = "";
    private string _version = "";
    private string _status = "";
    private string? _homePage;
    private string _requirementSummary = "";
    private IReadOnlyList<(string Id, string State)> requirements = [];

    public required ILocalizationService Localization { get; init; }
    public required string Id { get; init; }
    public required string Name { get => _name; init => _name = value; }
    public required string Version { get => _version; init => _version = value; }
    public required string Path { get; init; }
    public required string Status { get => _status; init => _status = value; }
    public string? HomePage { get => _homePage; init => _homePage = value; }
    public string RequirementSummary => _requirementSummary;
    public Visibility RequirementVisibility => string.IsNullOrWhiteSpace(RequirementSummary)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (SetProperty(ref _enabled, value)) RaisePropertyChanged(nameof(ToggleLabel));
        }
    }
    public string ToggleLabel => Localization[Enabled ? "Mods_On" : "Mods_Off"];

    public void UpdateFrom(ModViewItem source)
    {
        SetProperty(ref _name, source.Name, nameof(Name));
        SetProperty(ref _version, source.Version, nameof(Version));
        SetProperty(ref _status, source.Status, nameof(Status));
        SetProperty(ref _homePage, source.HomePage, nameof(HomePage));
        SetRequirements(source.requirements);
        Enabled = source.Enabled;
    }

    public void SetRequirements(IReadOnlyList<(string Id, string State)> value)
    {
        requirements = value;
        RefreshLocalizedText();
    }

    public void RefreshLocalizedText()
    {
        RaisePropertyChanged(nameof(ToggleLabel));
        var summary = string.Join(", ", requirements.Select(item =>
            $"{item.Id}: {Localization[RequirementKey(item.State)]}"));
        if (SetProperty(ref _requirementSummary, summary, nameof(RequirementSummary)))
            RaisePropertyChanged(nameof(RequirementVisibility));
    }

    private static string RequirementKey(string state) => state switch
    {
        "Missing" => "Requirement_Missing",
        "Inactive" => "Requirement_Inactive",
        "Outdated" => "Requirement_Outdated",
        _ => state
    };
}

internal sealed record ModImportPreview(
    string ZipPath,
    string Id,
    string Name,
    string Version,
    bool AlreadyInstalled);

internal sealed record LogEntry(string Level, string Message, DateTimeOffset Timestamp)
{
    public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Message}";
}
