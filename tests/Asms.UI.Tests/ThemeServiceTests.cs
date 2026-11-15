using Asms.UI.Shared.Extensions;
using Asms.UI.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Asms.UI.Tests;

public sealed class ThemeServiceTests
{
    [Fact]
    public void Theme_starts_in_system_mode_without_a_data_theme()
    {
        using var context = new Bunit.BunitContext();
        context.Services.AddAsmsUi();
        var theme = context.Services.GetRequiredService<IThemeService>();

        Assert.Equal(ThemeMode.System, theme.Mode);
        Assert.Null(theme.DataTheme);
    }

    [Fact]
    public void Changing_mode_updates_data_theme_and_notifies_once()
    {
        var theme = new ThemeService();
        var eventCount = 0;
        theme.ThemeChanged += () => eventCount++;

        theme.SetMode(ThemeMode.Light);
        Assert.Equal(ThemeMode.Light, theme.Mode);
        Assert.Equal("light", theme.DataTheme);
        Assert.Equal(1, eventCount);

        theme.SetMode(ThemeMode.Dark);
        Assert.Equal("dark", theme.DataTheme);
        Assert.Equal(2, eventCount);
    }

    [Fact]
    public void Setting_the_same_mode_does_not_notify()
    {
        var theme = new ThemeService();
        var eventCount = 0;
        theme.ThemeChanged += () => eventCount++;

        theme.SetMode(ThemeMode.System);
        theme.SetMode(ThemeMode.Light);
        theme.SetMode(ThemeMode.Light);

        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Concurrent_mode_changes_do_not_throw()
    {
        var theme = new ThemeService();
        var modes = new[] { ThemeMode.System, ThemeMode.Light, ThemeMode.Dark };

        var exception = Record.Exception(() =>
            Parallel.For(0, 1000, index =>
            {
                theme.SetMode(modes[index % modes.Length]);
                _ = theme.Mode;
                _ = theme.DataTheme;
            }));

        Assert.Null(exception);
    }
}
