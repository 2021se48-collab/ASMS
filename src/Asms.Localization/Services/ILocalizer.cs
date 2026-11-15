using Asms.Localization.Languages;

namespace Asms.Localization.Services;

public interface ILocalizer
{
    string this[string key] { get; }

    AppLanguage CurrentLanguage { get; }

    bool IsRightToLeft { get; }

    string Direction { get; }

    event Action? LanguageChanged;

    string Get(string key, params object[] args);

    void SetLanguage(string code);
}
