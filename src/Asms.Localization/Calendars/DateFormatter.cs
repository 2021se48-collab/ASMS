using System.Globalization;
using System.Text;
using Asms.Localization.Languages;

namespace Asms.Localization.Calendars;

public sealed class DateFormatter
{
    private const string PersianDigits = "۰۱۲۳۴۵۶۷۸۹";
    private const string ArabicIndicDigits = "٠١٢٣٤٥٦٧٨٩";

    public string FormatDate(
        DateOnly date,
        AppLanguage language,
        CalendarKind calendar,
        DigitStyle digits)
    {
        ArgumentNullException.ThrowIfNull(language);

        var (year, month, day, monthName) = GetParts(date, language, calendar);
        var formatted = $"{day.ToString(CultureInfo.InvariantCulture)} {monthName} {year.ToString(CultureInfo.InvariantCulture)}";
        return ConvertDigits(formatted, digits);
    }

    public string FormatNumeric(DateOnly date, CalendarKind calendar, DigitStyle digits)
    {
        var (year, month, day, _) = GetParts(date, SupportedLanguages.English, calendar);
        var formatted = string.Create(
            CultureInfo.InvariantCulture,
            $"{year:0000}/{month:00}/{day:00}");
        return ConvertDigits(formatted, digits);
    }

    public string ConvertDigits(string text, DigitStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (style == DigitStyle.Persian && character is >= '0' and <= '9')
            {
                builder.Append(PersianDigits[character - '0']);
            }
            else if (style == DigitStyle.Western &&
                (character is >= '\u06F0' and <= '\u06F9' ||
                 character is >= '\u0660' and <= '\u0669'))
            {
                builder.Append((char)('0' + GetDigitValue(character)));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    public bool TryParseNumeric(string? text, CalendarKind calendar, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = NormalizeDigits(text.Trim());
        var components = normalized.Split(['/', '-', '.'], StringSplitOptions.None);
        if (components.Length != 3 ||
            !int.TryParse(components[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(components[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
            !int.TryParse(components[2], NumberStyles.None, CultureInfo.InvariantCulture, out var day))
        {
            return false;
        }

        if (calendar == CalendarKind.Gregorian)
        {
            try
            {
                date = new DateOnly(year, month, day);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        if (calendar != CalendarKind.SolarHijri ||
            !SolarHijriDate.TryCreate(year, month, day, out var solarDate))
        {
            return false;
        }

        try
        {
            date = SolarHijriCalendarService.ToGregorian(solarDate);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            date = default;
            return false;
        }
    }

    private static (int Year, int Month, int Day, string MonthName) GetParts(
        DateOnly date,
        AppLanguage language,
        CalendarKind calendar)
    {
        if (calendar == CalendarKind.Gregorian)
        {
            return (
                date.Year,
                date.Month,
                date.Day,
                AfghanMonthNames.GetGregorianMonthName(language.Code, date.Month));
        }

        if (calendar != CalendarKind.SolarHijri)
        {
            throw new ArgumentOutOfRangeException(nameof(calendar));
        }

        try
        {
            var solarDate = SolarHijriCalendarService.FromGregorian(date);
            return (
                solarDate.Year,
                solarDate.Month,
                solarDate.Day,
                AfghanMonthNames.GetSolarHijriMonthName(language.Code, solarDate.Month));
        }
        catch (ArgumentOutOfRangeException)
        {
            return (
                date.Year,
                date.Month,
                date.Day,
                AfghanMonthNames.GetGregorianMonthName(language.Code, date.Month));
        }
    }

    private static string NormalizeDigits(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            builder.Append(character switch
            {
                >= '\u06F0' and <= '\u06F9' => (char)('0' + character - '\u06F0'),
                >= '\u0660' and <= '\u0669' => (char)('0' + character - '\u0660'),
                _ => character
            });
        }

        return builder.ToString();
    }

    private static int GetDigitValue(char digit) =>
        digit is >= '\u06F0' and <= '\u06F9'
            ? digit - '\u06F0'
            : digit - '\u0660';
}
