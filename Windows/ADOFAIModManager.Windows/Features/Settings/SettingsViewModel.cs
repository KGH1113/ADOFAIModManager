using ADOFAIModManager.Windows.Application.Localization;
using ADOFAIModManager.Windows.Models;

namespace ADOFAIModManager.Windows.Features.Settings;

internal sealed class SettingsViewModel : ObservableObject
{
    private readonly ILocalizationService localization;

    public SettingsViewModel(ILocalizationService localization)
    {
        this.localization = localization;
        Localization = localization;
        localization.LanguageChanged += (_, _) => RaisePropertyChanged(nameof(SelectedLanguageIndex));
    }

    public ILocalizationService Localization { get; }

    public int SelectedLanguageIndex
    {
        get => localization.SelectedLanguage switch
        {
            AppLanguage.Korean => 1,
            AppLanguage.English => 2,
            AppLanguage.SimplifiedChinese => 3,
            _ => 0
        };
        set => localization.SelectLanguage(value switch
        {
            1 => AppLanguage.Korean,
            2 => AppLanguage.English,
            3 => AppLanguage.SimplifiedChinese,
            _ => AppLanguage.System
        });
    }
}

