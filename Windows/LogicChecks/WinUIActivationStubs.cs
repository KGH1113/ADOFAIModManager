using Microsoft.UI.Xaml;

namespace ADOFAIModManager.Windows;

public partial class App
{
    private void InitializeComponent() { }
}

public sealed class MainWindow : Window
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task HandleModFileAsync(string path) => Task.CompletedTask;
}
