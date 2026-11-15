using System.Globalization;
using Asms.Localization.Calendars;
using Asms.Localization.Languages;

namespace Asms.Localization.Tests;

public sealed class SolarHijriCalendarTests
{
    private readonly DateFormatter _formatter = new();

    [Theory]
    [InlineData(2026, 3, 21, 1405, 1, 1)]
    [InlineData(2025, 3, 21, 1404, 1, 1)]
    [InlineData(2024, 3, 20, 1403, 1, 1)]
    [InlineData(2000, 1, 1, 1378, 10, 11)]
    [InlineData(1970, 1, 1, 1348, 10, 11)]
    public void Known_dates_convert_to_the_expected_solar_hijri_date(
        int gregorianYear,
        int gregorianMonth,
        int gregorianDay,
        int solarYear,
        int solarMonth,
        int solarDay)
    {
        // Reference conversions were checked against .NET PersianCalendar, an independent implementation.
        var actual = SolarHijriCalendarService.FromGregorian(
            new DateOnly(gregorianYear, gregorianMonth, gregorianDay));

        Assert.Equal(new SolarHijriDate(solarYear, solarMonth, solarDay), actual);
    }

    [Theory]
    [InlineData(2026, 3, 20, 1404, 12, 29)]
    [InlineData(2025, 3, 20, 1403, 12, 30)]
    [InlineData(2024, 3, 19, 1402, 12, 29)]
    public void Day_before_nowruz_is_the_correct_last_day_of_hoot(
        int gregorianYear,
        int gregorianMonth,
        int gregorianDay,
        int solarYear,
        int solarMonth,
        int solarDay)
    {
        var actual = SolarHijriCalendarService.FromGregorian(
            new DateOnly(gregorianYear, gregorianMonth, gregorianDay));

        Assert.Equal(new SolarHijriDate(solarYear, solarMonth, solarDay), actual);
    }

    [Theory]
    [InlineData(1399, true)]
    [InlineData(1400, false)]
    [InlineData(1401, false)]
    [InlineData(1402, false)]
    [InlineData(1403, true)]
    public void Leap_year_table_is_correct(int year, bool expected) =>
        Assert.Equal(expected, SolarHijriCalendarService.IsLeapYear(year));

    [Fact]
    public void Solar_hijri_dates_match_persian_calendar_for_every_day_from_1300_to_1500()
    {
        // PersianCalendar is used only as an independent test cross-check.
        var reference = new PersianCalendar();

        for (var year = 1300; year <= 1500; year++)
        {
            Assert.Equal(reference.IsLeapYear(year), SolarHijriCalendarService.IsLeapYear(year));
            for (var month = 1; month <= 12; month++)
            {
                var days = reference.GetDaysInMonth(year, month);
                Assert.Equal(days, SolarHijriCalendarService.DaysInMonth(year, month));
                for (var day = 1; day <= days; day++)
                {
                    var expectedGregorian = DateOnly.FromDateTime(
                        reference.ToDateTime(year, month, day, 0, 0, 0, 0));
                    var solarDate = new SolarHijriDate(year, month, day);
                    var actualGregorian = SolarHijriCalendarService.ToGregorian(solarDate);
                    Assert.Equal(expectedGregorian, actualGregorian);
                    Assert.Equal(solarDate, SolarHijriCalendarService.FromGregorian(actualGregorian));
                }
            }
        }
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(10000, 1, 1)]
    [InlineData(1400, 13, 1)]
    [InlineData(1400, 0, 1)]
    [InlineData(1400, 1, 0)]
    [InlineData(1400, 7, 31)]
    [InlineData(1400, 12, 30)]
    public void Invalid_solar_dates_are_rejected(int year, int month, int day)
    {
        Assert.False(SolarHijriDate.TryCreate(year, month, day, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => SolarHijriDate.Create(year, month, day));
    }

    [Theory]
    [InlineData(1399, 12, 30, true)]
    [InlineData(1403, 12, 30, true)]
    [InlineData(1400, 12, 29, true)]
    public void Hoot_day_30_is_valid_only_in_a_leap_year(
        int year,
        int month,
        int day,
        bool expected)
    {
        Assert.Equal(expected, SolarHijriDate.TryCreate(year, month, day, out _));
    }

    [Fact]
    public void Month_tables_have_twelve_unique_nonempty_names()
    {
        foreach (var language in new[] { "en", "ps", "fa-AF" })
        {
            var solarNames = Enumerable.Range(1, 12)
                .Select(month => AfghanMonthNames.GetSolarHijriMonthName(language, month))
                .ToArray();
            var gregorianNames = Enumerable.Range(1, 12)
                .Select(month => AfghanMonthNames.GetGregorianMonthName(language, month))
                .ToArray();

            Assert.Equal(12, solarNames.Length);
            Assert.Equal(12, gregorianNames.Length);
            Assert.All(solarNames.Concat(gregorianNames), name =>
                Assert.False(string.IsNullOrWhiteSpace(name)));
            Assert.Equal(12, solarNames.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(12, gregorianNames.Distinct(StringComparer.Ordinal).Count());
        }

        foreach (var language in new[] { "ps", "fa-AF" })
        {
            foreach (var name in Enumerable.Range(1, 12)
                         .Select(month => AfghanMonthNames.GetSolarHijriMonthName(language, month))
                         .Concat(Enumerable.Range(1, 12)
                             .Select(month => AfghanMonthNames.GetGregorianMonthName(language, month))))
            {
                Assert.Contains(name, value => value is >= '\u0600' and <= '\u06FF');
            }
        }
    }

    [Fact]
    public void Weekdays_are_in_afghan_order_and_have_seven_names_per_language()
    {
        Assert.Equal(DayOfWeek.Saturday, AfghanMonthNames.FirstDayOfWeek);

        foreach (var language in new[] { "en", "ps", "fa-AF" })
        {
            var names = Enum.GetValues<DayOfWeek>()
                .Select(day => AfghanMonthNames.GetWeekdayName(language, day))
                .ToArray();
            Assert.Equal(7, names.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(
                language == "en" ? "Saturday" : language == "ps" ? "خالي" : "شنبه",
                AfghanMonthNames.GetWeekdayName(language, DayOfWeek.Saturday));
        }
    }

    [Fact]
    public void Date_formatter_formats_solar_hijri_and_gregorian_dates()
    {
        var nowruz = new DateOnly(2026, 3, 21);

        Assert.Equal(
            "1 Hamal 1405",
            _formatter.FormatDate(
                nowruz,
                SupportedLanguages.English,
                CalendarKind.SolarHijri,
                DigitStyle.Western));
        Assert.Equal(
            "۱ حمل ۱۴۰۵",
            _formatter.FormatDate(
                nowruz,
                SupportedLanguages.Dari,
                CalendarKind.SolarHijri,
                DigitStyle.Persian));
        Assert.Equal(
            "۱ وری ۱۴۰۵",
            _formatter.FormatDate(
                nowruz,
                SupportedLanguages.Pashto,
                CalendarKind.SolarHijri,
                DigitStyle.Persian));
        Assert.Equal(
            "21 March 2026",
            _formatter.FormatDate(
                nowruz,
                SupportedLanguages.English,
                CalendarKind.Gregorian,
                DigitStyle.Western));
        Assert.Equal(
            "۲۱ مارچ ۲۰۲۶",
            _formatter.FormatDate(
                nowruz,
                SupportedLanguages.Dari,
                CalendarKind.Gregorian,
                DigitStyle.Persian));
        Assert.Equal("1405/01/01", _formatter.FormatNumeric(nowruz, CalendarKind.SolarHijri, DigitStyle.Western));
        Assert.Equal("۲۰۲۶/۰۳/۲۱", _formatter.FormatNumeric(nowruz, CalendarKind.Gregorian, DigitStyle.Persian));
    }

    [Fact]
    public void Digit_conversion_round_trips_and_preserves_other_characters()
    {
        const string western = "1405/12/30 Hamal";
        const string persian = "۱۴۰۵/۱۲/۳۰ Hamal";

        Assert.Equal(persian, _formatter.ConvertDigits(western, DigitStyle.Persian));
        Assert.Equal(western, _formatter.ConvertDigits(persian, DigitStyle.Western));
        Assert.Equal("٢٠٢٦", _formatter.ConvertDigits("٢٠٢٦", DigitStyle.Persian));
        Assert.Equal("2026", _formatter.ConvertDigits("٢٠٢٦", DigitStyle.Western));
    }

    [Theory]
    [InlineData("۱۴۰۵-01.۰۱", CalendarKind.SolarHijri, 2026, 3, 21)]
    [InlineData("۲۰۲۶/03/٢١", CalendarKind.Gregorian, 2026, 3, 21)]
    [InlineData("2026-03-21", CalendarKind.Gregorian, 2026, 3, 21)]
    public void Try_parse_accepts_mixed_digits_and_supported_separators(
        string text,
        CalendarKind calendar,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        Assert.True(_formatter.TryParseNumeric(text, calendar, out var date));
        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), date);
    }

    [Theory]
    [InlineData("abc", CalendarKind.SolarHijri)]
    [InlineData("", CalendarKind.SolarHijri)]
    [InlineData("1405/13/01", CalendarKind.SolarHijri)]
    [InlineData("1405/01/32", CalendarKind.SolarHijri)]
    [InlineData("2026/13/01", CalendarKind.Gregorian)]
    [InlineData("2026/01/32", CalendarKind.Gregorian)]
    public void Try_parse_rejects_invalid_values(string text, CalendarKind calendar)
    {
        Assert.False(_formatter.TryParseNumeric(text, calendar, out _));
    }

    [Fact]
    public void Try_parse_rejects_null_without_throwing()
    {
        Assert.False(_formatter.TryParseNumeric(null, CalendarKind.SolarHijri, out _));
    }

    [Fact]
    public void Date_formatter_handles_dateonly_extremes_in_both_calendars()
    {
        var dates = new[] { DateOnly.MinValue, DateOnly.MaxValue };
        foreach (var date in dates)
        {
            foreach (var calendar in Enum.GetValues<CalendarKind>())
            {
                Assert.False(string.IsNullOrWhiteSpace(
                    _formatter.FormatDate(date, SupportedLanguages.English, calendar, DigitStyle.Western)));
                Assert.False(string.IsNullOrWhiteSpace(
                    _formatter.FormatNumeric(date, calendar, DigitStyle.Persian)));
            }
        }
    }
}
