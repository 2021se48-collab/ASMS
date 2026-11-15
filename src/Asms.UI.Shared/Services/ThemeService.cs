namespace Asms.UI.Shared.Services;

public sealed class ThemeService : IThemeService
{
    private readonly object _gate = new();

    // Saving this setting comes with the Settings screen.
    private ThemeMode _mode = ThemeMode.System;

    public event Action? ThemeChanged;

    public ThemeMode Mode
    {
        get
        {
            lock (_gate)
            {
                return _mode;
            }
        }
    }

    public string? DataTheme => Mode switch
    {
        ThemeMode.System => null,
        ThemeMode.Light => "light",
        ThemeMode.Dark => "dark",
        _ => throw new InvalidOperationException("The theme mode is invalid.")
    };

    public void SetMode(ThemeMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        Action? handlers;
        lock (_gate)
        {
            if (_mode == mode)
            {
                return;
            }

            _mode = mode;
            handlers = ThemeChanged;
        }

        handlers?.Invoke();
    }
}
