namespace Asms.Localization.Calendars;

public readonly record struct SolarHijriDate(int Year, int Month, int Day)
{
    public static bool TryCreate(int year, int month, int day, out SolarHijriDate date)
    {
        date = default;
        if (year is < 1 or > 9999 || month is < 1 or > 12)
        {
            return false;
        }

        var daysInMonth = SolarHijriCalendarService.DaysInMonth(year, month);
        if (day < 1 || day > daysInMonth)
        {
            return false;
        }

        date = new SolarHijriDate(year, month, day);
        return true;
    }

    public static SolarHijriDate Create(int year, int month, int day)
    {
        if (TryCreate(year, month, day, out var date))
        {
            return date;
        }

        throw new ArgumentOutOfRangeException(
            nameof(day),
            "The Solar Hijri date is outside its valid range.");
    }
}
