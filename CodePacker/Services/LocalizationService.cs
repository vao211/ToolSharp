using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CodePacker.Services;

public record LanguageOption(string FileName, string DisplayName);

public class LocalizationService : INotifyPropertyChanged
{
    private Dictionary<string, string> _strings = new();
    private Dictionary<string, string> _fallback = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string LangFolder => Path.Combine(AppContext.BaseDirectory, "lang");
    public List<LanguageOption> AvailableLanguages { get; private set; } = [];

    public string this[string key] =>
        _strings.TryGetValue(key, out var val) ? val :
        _fallback.TryGetValue(key, out var fb) ? fb : key;

    public LocalizationService()
    {
        ScanLanguages();
        LoadFallback();
    }

    public void ScanLanguages()
    {
        AvailableLanguages.Clear();
        if (!Directory.Exists(LangFolder))
            Directory.CreateDirectory(LangFolder);

        foreach (var file in Directory.EnumerateFiles(LangFolder, "*.json"))
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                if (dict is null) continue;

                var fileName = Path.GetFileName(file);
                var displayName = dict.TryGetValue("language_name", out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : Path.GetFileNameWithoutExtension(file);

                AvailableLanguages.Add(new LanguageOption(fileName, displayName));
            }
            catch {}
        }

        AvailableLanguages = [.. AvailableLanguages.OrderBy(x => x.DisplayName)];
    }

    private void LoadFallback()
    {
        var enPath = Path.Combine(LangFolder, "en_us.json");
        if (File.Exists(enPath))
        {
            try { _fallback = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(enPath)) ?? new(); }
            catch { }
        }
    }

    public void LoadLanguage(string fileName)
    {
        var path = Path.Combine(LangFolder, fileName);
        if (!File.Exists(path) && AvailableLanguages.Count > 0)
            path = Path.Combine(LangFolder, AvailableLanguages[0].FileName);

        if (File.Exists(path))
        {
            try
            {
                _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new();
            }
            catch { _strings = new(); }
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}