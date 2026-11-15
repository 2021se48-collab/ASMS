namespace Asms.UI.Shared.Services;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public interface IThemeService
{
    ThemeMode Mode { get; }

    string? DataTheme { get; }

    event Action? ThemeChanged;

    void SetMode(ThemeMode mode);
}
