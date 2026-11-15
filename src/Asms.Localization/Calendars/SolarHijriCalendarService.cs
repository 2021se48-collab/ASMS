namespace Asms.Localization.Calendars;

public static class SolarHijriCalendarService
{
    // The 33-year cycle has leap years at these year remainders; year 1405 anchors to Nowruz 2026.
    private static readonly int[] LeapYearRemainders = [1, 5, 9, 13, 17, 22, 26, 30];
    private static readonly DateOnly AnchorDate = new(2026, 3, 21);
    private const int AnchorYear = 1405;

    public static DateOnly ToGregorian(SolarHijriDate date)
    {
        if (!SolarHijriDate.TryCreate(date.Year, date.Month, date.Day, out _))
        {
            throw new ArgumentOutOfRangeException(nameof(date), "The Solar Hijri date is invalid.");
        }

        var startOfYear = GetStartOfYear(date.Year);
        var daysBeforeMonth = date.Month <= 6
            ? (date.Month - 1) * 31
            : 186 + ((date.Month - 7) * 30);
        return startOfYear.AddDays(daysBeforeMonth + date.Day - 1);
    }

    public static SolarHijriDate FromGregorian(DateOnly date)
    {
        var solarYear = date.Year - 621;
        if (solarYear < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(date),
                "The date is before the first representable Solar Hijri year.");
        }

        var startOfYear = GetStartOfYear(solarYear);
        if (date < startOfYear)
        {
            solarYear--;
            if (solarYear < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(date),
                    "The date is before the first representable Solar Hijri year.");
            }

            startOfYear = GetStartOfYear(solarYear);
        }
        else
        {
            var nextStart = GetStartOfYear(solarYear + 1);
            if (date >= nextStart)
            {
                solarYear++;
                startOfYear = nextStart;
            }
        }

        var dayOfYear = date.DayNumber - startOfYear.DayNumber;
        var month = dayOfYear < 186
            ? (dayOfYear / 31) + 1
            : ((dayOfYear - 186) / 30) + 7;
        var day = dayOfYear < 186
            ? (dayOfYear % 31) + 1
            : ((dayOfYear - 186) % 30) + 1;
        return SolarHijriDate.Create(solarYear, month, day);
    }

    public static bool IsLeapYear(int solarYear)
    {
        ValidateYear(solarYear);
        var remainder = solarYear % 33;
        return Array.BinarySearch(LeapYearRemainders, remainder) >= 0;
    }

    public static int DaysInMonth(int year, int month)
    {
        ValidateYear(year);
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        return month switch
        {
            <= 6 => 31,
            <= 11 => 30,
            _ => IsLeapYear(year) ? 30 : 29
        };
    }

    private static DateOnly GetStartOfYear(int year)
    {
        ValidateYear(year);
        var days = DaysBeforeYear(year) - DaysBeforeYear(AnchorYear);
        return AnchorDate.AddDays(checked((int)days));
    }

    private static long DaysBeforeYear(int year) =>
        ((long)year - 1) * 365 + CountLeapYearsThrough(year - 1);

    private static long CountLeapYearsThrough(int year)
    {
        if (year <= 0)
        {
            return 0;
        }

        var fullCycles = year / 33;
        var remainder = year % 33;
        return (fullCycles * 8) +
            LeapYearRemainders.Count(leapRemainder => leapRemainder <= remainder);
    }

    private static void ValidateYear(int year)
    {
        if (year is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }
    }
}
