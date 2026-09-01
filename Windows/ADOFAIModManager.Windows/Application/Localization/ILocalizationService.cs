using System.ComponentModel;

namespace ADOFAIModManager.Windows.Application.Localization;

internal interface IAppLanguageStore
{
    AppLanguage LoadLanguage();
    void SaveLanguage(AppLanguage language);
    string? LoadGamePath();
    Task SaveGamePathAsync(string path);
}

internal interface ILocalizationService : INotifyPropertyChanged
{
    event EventHandler? LanguageChanged;
    AppLanguage SelectedLanguage { get; }
    AppLanguage EffectiveLanguage { get; }
    string this[string key] { get; }
    string Format(string key, params object[] arguments);
    void SelectLanguage(AppLanguage language);
}

