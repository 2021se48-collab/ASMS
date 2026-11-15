using System.Globalization;
using System.Resources;
using Asms.Localization.Languages;

namespace Asms.Localization.Services;

public sealed class Localizer : ILocalizer
{
    private readonly ResourceManager _resourceManager;
    private readonly object _languageLock = new();
    private AppLanguage _currentLanguage = SupportedLanguages.Default;

    public Localizer(ResourceManager? resourceManager = null)
    {
        _resourceManager = resourceManager ??
            new ResourceManager("Asms.Localization.Resources.SharedResource", typeof(SharedResource).Assembly);
    }

    public string this[string key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);
            var language = CurrentLanguage;
            var culture = SupportedLanguages.ToCulture(language);
            var value = TryGetString(key, culture);

            if (string.IsNullOrEmpty(value) &&
                !language.Code.Equals(SupportedLanguages.English.Code, StringComparison.Ordinal))
            {
                value = TryGetString(key, SupportedLanguages.ToCulture(SupportedLanguages.English));
            }

            return string.IsNullOrEmpty(value) ? $"[{key}]" : value;
        }
    }

    public AppLanguage CurrentLanguage
    {
        get
        {
            lock (_languageLock)
            {
                return _currentLanguage;
            }
        }
    }

    public bool IsRightToLeft => CurrentLanguage.IsRightToLeft;

    public string Direction => IsRightToLeft ? "rtl" : "ltr";

    public event Action? LanguageChanged;

    public string Get(string key, params object[] args)
    {
        var value = this[key];
        if (args.Length == 0)
        {
            return value;
        }

        try
        {
            return string.Format(SupportedLanguages.ToCulture(CurrentLanguage), value, args);
        }
        catch (FormatException)
        {
            return value;
        }
    }

    public void SetLanguage(string code)
    {
        var nextLanguage = SupportedLanguages.Resolve(code);
        Action? languageChanged;
        lock (_languageLock)
        {
            if (_currentLanguage == nextLanguage)
            {
                return;
            }

            _currentLanguage = nextLanguage;
            languageChanged = LanguageChanged;
        }

        languageChanged?.Invoke();
    }

    private string? TryGetString(string key, CultureInfo culture)
    {
        try
        {
            return _resourceManager.GetString(key, culture);
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
    }
}
