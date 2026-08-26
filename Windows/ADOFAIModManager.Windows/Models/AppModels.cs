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

    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Path { get; init; }
    public required string Status { get; init; }
    public required bool Installed { get; init; }
    public string? HomePage { get; init; }
    public string RequirementSummary { get; init; } = "";
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
