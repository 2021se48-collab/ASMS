using System.Globalization;

namespace Asms.Localization.Languages;

public sealed record AppLanguage(string Code, string DisplayKey, bool IsRightToLeft);

public static class SupportedLanguages
{
    public static AppLanguage English { get; } = new("en", "Lang.English", false);

    public static AppLanguage Pashto { get; } = new("ps", "Lang.Pashto", true);

    public static AppLanguage Dari { get; } = new("fa-AF", "Lang.Dari", true);

    public static IReadOnlyList<AppLanguage> All { get; } =
        Array.AsReadOnly(new[] { English, Pashto, Dari });

    public static AppLanguage Default => English;

    public static AppLanguage Resolve(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Default;
        }

        var normalized = code.Trim();
        if (normalized.Equals("fa", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("prs", StringComparison.OrdinalIgnoreCase))
        {
            return Dari;
        }

        if (normalized.Equals("pa-AF", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ps-AF", StringComparison.OrdinalIgnoreCase))
        {
            return Pashto;
        }

        return All.FirstOrDefault(language =>
            language.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase)) ?? Default;
    }

    public static CultureInfo ToCulture(AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);

        var fallback = language.Code switch
        {
            "fa-AF" => "fa",
            "ps" => "ps",
            _ => string.Empty
        };

        try
        {
            return CultureInfo.GetCultureInfo(language.Code);
        }
        catch (CultureNotFoundException)
        {
            if (fallback.Length == 0)
            {
                return CultureInfo.InvariantCulture;
            }
        }

        try
        {
            return CultureInfo.GetCultureInfo(fallback);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
