using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    private bool _installed;
    private string? _homePage;
    private string _requirementSummary = "";

    public required string Id { get; init; }
    public required string Name { get => _name; init => _name = value; }
    public required string Version { get => _version; init => _version = value; }
    public required string Path { get; init; }
    public required string Status { get => _status; init => _status = value; }
    public required bool Installed { get => _installed; init => _installed = value; }
    public string? HomePage { get => _homePage; init => _homePage = value; }
    public string RequirementSummary { get => _requirementSummary; init => _requirementSummary = value; }
    public Visibility InstalledVisibility => Installed ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RemovedVisibility => Installed ? Visibility.Collapsed : Visibility.Visible;
    public Visibility RequirementVisibility => string.IsNullOrWhiteSpace(RequirementSummary)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public void UpdateFrom(ModViewItem source)
    {
        SetProperty(ref _name, source.Name, nameof(Name));
        SetProperty(ref _version, source.Version, nameof(Version));
        SetProperty(ref _status, source.Status, nameof(Status));
        SetProperty(ref _homePage, source.HomePage, nameof(HomePage));
        if (SetProperty(ref _installed, source.Installed, nameof(Installed)))
        {
            RaisePropertyChanged(nameof(InstalledVisibility));
            RaisePropertyChanged(nameof(RemovedVisibility));
        }
        if (SetProperty(ref _requirementSummary, source.RequirementSummary, nameof(RequirementSummary)))
            RaisePropertyChanged(nameof(RequirementVisibility));
        Enabled = source.Enabled;
    }
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
