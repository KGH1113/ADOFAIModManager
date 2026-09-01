using ADOFAIModManager.Windows.Application.Localization;
using ADOFAIModManager.Windows.Features.Settings;
using Microsoft.UI.Xaml.Controls;

namespace ADOFAIModManager.Windows.Views;

public sealed partial class SettingsPage : Page
{
    internal SettingsPage(ILocalizationService localization)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(localization);
    }
}

