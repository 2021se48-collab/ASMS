using Asms.Localization.Languages;

namespace Asms.Localization.Calendars;

// DRAFT - needs review by a native speaker
public static class AfghanMonthNames
{
    private static readonly string[] SolarHijriEnglish =
    [
        "Hamal", "Sawr", "Jawza", "Saratan", "Asad", "Sunbula",
        "Mizan", "Aqrab", "Qaws", "Jadi", "Dalvo", "Hoot"
    ];

    private static readonly string[] SolarHijriDari =
    [
        "حمل", "ثور", "جوزا", "سرطان", "اسد", "سنبله",
        "میزان", "عقرب", "قوس", "جدی", "دلو", "حوت"
    ];

    private static readonly string[] SolarHijriPashto =
    [
        "وری", "غویی", "غبرګولی", "چنګاښ", "زمری", "وږی",
        "تله", "لړم", "لیندۍ", "مرغومی", "سلواغه", "کب"
    ];

    private static readonly string[] GregorianEnglish =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    ];

    private static readonly string[] GregorianDari =
    [
        "جنوری", "فبروری", "مارچ", "اپریل", "می", "جون",
        "جولای", "اگست", "سپتمبر", "اکتوبر", "نومبر", "دسمبر"
    ];

    private static readonly string[] GregorianPashto =
    [
        "جنوري", "فبروري", "مارچ", "اپریل", "می", "جون",
        "جولای", "اګست", "سپتمبر", "اکتوبر", "نومبر", "دسمبر"
    ];

    private static readonly string[] WeekdaysEnglish =
    [
        "Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday"
    ];

    private static readonly string[] WeekdaysDari =
    [
        "شنبه", "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه"
    ];

    private static readonly string[] WeekdaysPashto =
    [
        "خالي", "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه"
    ];

    public static DayOfWeek FirstDayOfWeek => DayOfWeek.Saturday;

    public static string GetSolarHijriMonthName(string? languageCode, int month) =>
        GetName(languageCode, month, SolarHijriEnglish, SolarHijriPashto, SolarHijriDari);

    public static string GetGregorianMonthName(string? languageCode, int month) =>
        GetName(languageCode, month, GregorianEnglish, GregorianPashto, GregorianDari);

    public static string GetWeekdayName(string? languageCode, DayOfWeek day)
    {
        if (day is < DayOfWeek.Sunday or > DayOfWeek.Saturday)
        {
            throw new ArgumentOutOfRangeException(nameof(day));
        }

        var index = ((int)day + 1) % 7;
        return Select(languageCode, WeekdaysEnglish, WeekdaysPashto, WeekdaysDari)[index];
    }

    private static string GetName(
        string? languageCode,
        int month,
        string[] english,
        string[] pashto,
        string[] dari)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        return Select(languageCode, english, pashto, dari)[month - 1];
    }

    private static string[] Select(
        string? languageCode,
        string[] english,
        string[] pashto,
        string[] dari) =>
        SupportedLanguages.Resolve(languageCode).Code switch
        {
            "ps" => pashto,
            "fa-AF" => dari,
            _ => english
        };
}
