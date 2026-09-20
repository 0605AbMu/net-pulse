using System.ComponentModel;
using NetPulse.Localization.Translations;

namespace NetPulse.Localization;

public class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    private string _currentLanguage = "uz";
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            var normalized = value?.ToLowerInvariant() ?? "uz";
            if (_translations.ContainsKey(normalized) && _currentLanguage != normalized)
            {
                _currentLanguage = normalized;
                OnPropertyChanged(string.Empty);
                OnPropertyChanged("Item[]");
                OnPropertyChanged(nameof(CurrentLanguage));
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string this[string key] => GetString(key);

    public LocalizationService()
    {
        _translations["uz"] = UzbekTranslation.GetDictionary();
        _translations["en"] = EnglishTranslation.GetDictionary();
        _translations["ru"] = RussianTranslation.GetDictionary();
    }

    public string GetString(string key, params object[] args)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        // 1. Try current language
        if (_translations.TryGetValue(_currentLanguage, out var dict) && dict.TryGetValue(key, out var val))
        {
            return FormatSafe(val, args);
        }

        // 2. Fallback to Uzbek
        if (_translations.TryGetValue("uz", out var uzDict) && uzDict.TryGetValue(key, out var uzVal))
        {
            return FormatSafe(uzVal, args);
        }

        // 3. Fallback to key itself
        return FormatSafe(key, args);
    }

    public static string Get(string key, params object[] args)
    {
        return Instance.GetString(key, args);
    }

    private static string FormatSafe(string template, object[]? args)
    {
        if (args == null || args.Length == 0) return template;
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
