using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ADOFAIModManager.Windows.Application.Localization;

namespace ADOFAIModManager.Windows.Infrastructure.Localization;

internal sealed class LocalizationService : ILocalizationService
{
    private readonly IAppLanguageStore store;
    private readonly Func<CultureInfo> systemCulture;
    private readonly IReadOnlyDictionary<AppLanguage, IReadOnlyDictionary<string, string>> resources;
    private AppLanguage selectedLanguage;

    public LocalizationService(IAppLanguageStore store, Func<CultureInfo>? systemCulture = null)
    {
        this.store = store;
        var detectedSystemCulture = CultureInfo.CurrentUICulture;
        this.systemCulture = systemCulture ?? (() => detectedSystemCulture);
        resources = new Dictionary<AppLanguage, IReadOnlyDictionary<string, string>>
        {
            [AppLanguage.English] = LoadResource("en"),
            [AppLanguage.Korean] = LoadResource("ko"),
            [AppLanguage.SimplifiedChinese] = LoadResource("zh-Hans")
        };
        var expectedKeys = resources[AppLanguage.English].Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var resource in resources.Values)
            if (!expectedKeys.SetEquals(resource.Keys))
                throw new InvalidDataException("Localization resources do not contain the same keys.");
        selectedLanguage = store.LoadLanguage();
        ApplyCulture();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public AppLanguage SelectedLanguage => selectedLanguage;
    public AppLanguage EffectiveLanguage => selectedLanguage == AppLanguage.System
        ? MapSystemCulture(systemCulture())
        : selectedLanguage;

    public string this[string key]
    {
        get
        {
            var active = resources[EffectiveLanguage];
            if (active.TryGetValue(key, out var value)) return value;
            return resources[AppLanguage.English].TryGetValue(key, out value) ? value : key;
        }
    }

    public string Format(string key, params object[] arguments) =>
        string.Format(CultureFor(EffectiveLanguage), this[key], arguments);

    public void SelectLanguage(AppLanguage language)
    {
        if (selectedLanguage == language) return;
        selectedLanguage = language;
        store.SaveLanguage(language);
        ApplyCulture();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    internal static AppLanguage MapSystemCulture(CultureInfo culture)
    {
        var name = culture.Name;
        if (name.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) return AppLanguage.Korean;
        if (name.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)
            || name.Equals("zh-SG", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase))
            return AppLanguage.SimplifiedChinese;
        return AppLanguage.English;
    }

    private void ApplyCulture()
    {
        var culture = CultureFor(EffectiveLanguage);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private static CultureInfo CultureFor(AppLanguage language) => language switch
    {
        AppLanguage.Korean => CultureInfo.GetCultureInfo("ko-KR"),
        AppLanguage.SimplifiedChinese => CultureInfo.GetCultureInfo("zh-CN"),
        _ => CultureInfo.GetCultureInfo("en-US")
    };

    private static IReadOnlyDictionary<string, string> LoadResource(string language)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var suffix = $"Strings.{language}.json";
        var name = assembly.GetManifestResourceNames()
            .Single(resource => resource.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing localization resource: {suffix}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException($"Invalid localization resource: {suffix}");
    }
}
