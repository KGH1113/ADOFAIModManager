using System.Text.Json;
using ADOFAIModManager.Windows.Application.Localization;

namespace ADOFAIModManager.Windows.Infrastructure.Localization;

internal sealed class JsonAppLanguageStore(string settingsPath) : IAppLanguageStore
{
    private readonly object gate = new();

    public AppLanguage LoadLanguage()
    {
        var settings = Read();
        return Enum.TryParse<AppLanguage>(settings.AppLanguage, ignoreCase: true, out var language)
            ? language
            : AppLanguage.System;
    }

    public void SaveLanguage(AppLanguage language)
    {
        lock (gate)
        {
            var settings = ReadCore();
            WriteCore(settings with { AppLanguage = language.ToString() });
        }
    }

    public string? LoadGamePath() => Read().GamePath;

    public Task SaveGamePathAsync(string path)
    {
        lock (gate)
        {
            var settings = ReadCore();
            WriteCore(settings with { GamePath = path });
        }
        return Task.CompletedTask;
    }

    private Settings Read()
    {
        lock (gate) return ReadCore();
    }

    private Settings ReadCore()
    {
        try
        {
            if (!File.Exists(settingsPath)) return new Settings();
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath), SerializerOptions)
                ?? new Settings();
        }
        catch
        {
            return new Settings();
        }
    }

    private void WriteCore(Settings settings)
    {
        var directory = Path.GetDirectoryName(settingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, SerializerOptions));
        File.Move(temporaryPath, settingsPath, overwrite: true);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private sealed record Settings(string? AppLanguage = null, string? GamePath = null);
}

