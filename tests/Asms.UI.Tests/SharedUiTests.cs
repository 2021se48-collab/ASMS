using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Asms.Contracts.Auth;
using Asms.Localization;
using Asms.Localization.Languages;
using Asms.Localization.Services;
using Asms.UI.Shared.Extensions;
using Asms.UI.Shared.Layout;
using Asms.UI.Shared.Pages;
using Asms.UI.Shared.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Asms.UI.Tests;

public sealed class SharedUiTests
{
    [Fact]
    public void Language_chooser_renders_all_three_localized_language_buttons()
    {
        using var context = CreateContext();

        var cut = context.Render<LanguageChooser>();

        Assert.Equal(3, cut.FindAll("button").Count);
        Assert.Contains("English", cut.Markup);
        Assert.Contains("پښتو", cut.Markup);
        Assert.Contains("دری", cut.Markup);
        Assert.Contains("Choose language", cut.Markup);
    }

    [Theory]
    [InlineData("English", "en", "ltr")]
    [InlineData("پښتو", "ps", "rtl")]
    [InlineData("دری", "fa-AF", "rtl")]
    public void Choosing_a_language_updates_localizer_direction_and_navigation(
        string languageName,
        string expectedCode,
        string expectedDirection)
    {
        using var context = CreateContext();
        var cut = context.Render<LanguageChooser>();

        cut.Find($"button[aria-label='{languageName}']").Click();

        var localizer = context.Services.GetRequiredService<ILocalizer>();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        Assert.Equal(expectedCode, localizer.CurrentLanguage.Code);
        Assert.Equal(expectedDirection == "rtl", localizer.IsRightToLeft);
        Assert.EndsWith("/home", navigation.Uri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en", "ltr")]
    [InlineData("ps", "rtl")]
    [InlineData("fa-AF", "rtl")]
    public void Main_layout_sets_root_language_and_direction_and_updates(
        string languageCode,
        string expectedDirection)
    {
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<ILocalizer>();
        var cut = context.Render<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, builder => builder.AddContent(0, "body")));

        localizer.SetLanguage(languageCode);

        cut.WaitForAssertion(() =>
        {
            var root = cut.Find(".app-shell");
            Assert.Equal(expectedDirection, root.GetAttribute("dir"));
            Assert.Equal(SupportedLanguages.Resolve(languageCode).Code, root.GetAttribute("lang"));
        });
    }

    [Fact]
    public void Main_layout_unsubscribes_from_language_changes_when_disposed()
    {
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<ILocalizer>();
        var cut = context.Render<MainLayout>();

        cut.Dispose();

        var exception = Record.Exception(() => localizer.SetLanguage("ps"));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(ThemeMode.System, null)]
    [InlineData(ThemeMode.Light, "light")]
    [InlineData(ThemeMode.Dark, "dark")]
    public void Main_layout_renders_the_selected_theme(
        ThemeMode mode,
        string? expectedDataTheme)
    {
        using var context = CreateContext();
        var theme = context.Services.GetRequiredService<IThemeService>();
        theme.SetMode(mode);

        var cut = context.Render<MainLayout>();

        Assert.Equal(expectedDataTheme, cut.Find(".app-shell").GetAttribute("data-theme"));
    }

    [Fact]
    public void Theme_button_cycles_system_light_dark_and_back_to_system()
    {
        using var context = CreateContext();
        var cut = context.Render<MainLayout>();
        var button = cut.Find(".theme-button");

        Assert.Equal("System", button.TextContent.Trim());

        button.Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("light", cut.Find(".app-shell").GetAttribute("data-theme"));
            Assert.Equal("Light", cut.Find(".theme-button").TextContent.Trim());
        });

        cut.Find(".theme-button").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal("dark", cut.Find(".app-shell").GetAttribute("data-theme"));
            Assert.Equal("Dark", cut.Find(".theme-button").TextContent.Trim());
        });

        cut.Find(".theme-button").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Null(cut.Find(".app-shell").GetAttribute("data-theme"));
            Assert.Equal("System", cut.Find(".theme-button").TextContent.Trim());
        });
    }

    [Theory]
    [InlineData(ThemeMode.System)]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void Main_layout_theme_button_shows_current_mode_and_toggle_accessible_name(
        ThemeMode mode)
    {
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<ILocalizer>();
        context.Services.GetRequiredService<IThemeService>().SetMode(mode);

        var cut = context.Render<MainLayout>();
        var button = cut.Find(".theme-button");

        Assert.Equal(localizer[$"Theme.{mode}"], button.TextContent.Trim());
        Assert.Equal(localizer["Theme.Toggle"], button.GetAttribute("aria-label"));
        Assert.Empty(button.QuerySelectorAll("svg"));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ps")]
    [InlineData("fa-AF")]
    public void Theme_button_text_and_accessible_name_follow_the_selected_language(
        string languageCode)
    {
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<ILocalizer>();
        localizer.SetLanguage(languageCode);
        var culture = CultureInfo.GetCultureInfo(languageCode);
        var resources = new ResourceManager(
            "Asms.Localization.Resources.SharedResource",
            typeof(SharedResource).Assembly);

        var cut = context.Render<MainLayout>();
        var button = cut.Find(".theme-button");

        Assert.Equal(resources.GetString("Theme.Toggle", culture), button.GetAttribute("aria-label"));
        Assert.Equal(resources.GetString("Theme.System", culture), button.TextContent.Trim());
    }

    [Fact]
    public void Main_layout_unsubscribes_from_theme_changes_when_disposed()
    {
        using var context = CreateContext();
        var theme = context.Services.GetRequiredService<IThemeService>();
        var cut = context.Render<MainLayout>();

        cut.Dispose();

        var exception = Record.Exception(() => theme.SetMode(ThemeMode.Dark));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ps")]
    [InlineData("fa-AF")]
    public void Home_shows_the_localized_dashboard_greeting_role_and_placeholder(string languageCode)
    {
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<ILocalizer>();
        localizer.SetLanguage(languageCode);
        var session = context.Services.GetRequiredService<IAuthSession>();
        session.SignIn(CreateLoginResponse(["Teacher"]), []);

        var cut = context.Render<Home>();
        var culture = CultureInfo.GetCultureInfo(languageCode);
        var resources = new ResourceManager(
            "Asms.Localization.Resources.SharedResource",
            typeof(SharedResource).Assembly);
        var keys = new[] { "Home.Greeting", "Role.Teacher", "Home.ComingSoon" };

        foreach (var key in keys)
        {
            var expected = resources.GetString(key, culture);
            Assert.False(string.IsNullOrEmpty(expected), $"{key} resource is missing.");
            Assert.Contains(expected, cut.Markup);
        }

        Assert.Equal(4, cut.FindAll(".dashboard-card").Count);
        Assert.All(
            cut.FindAll(".dashboard-card .empty-state"),
            state => Assert.Contains(localizer["Home.ComingSoon"], state.TextContent));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ps")]
    [InlineData("fa-AF")]
    public void App_shell_shows_localized_role_badges(string languageCode)
    {
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<ILocalizer>();
        localizer.SetLanguage(languageCode);
        context.Services.GetRequiredService<IAuthSession>()
            .SignIn(CreateLoginResponse(["SchoolOwner", "Teacher"]), []);

        var cut = context.Render<AppShell>();
        var badges = cut.FindAll(".role-badge");

        Assert.Equal(2, badges.Count);
        Assert.Equal(localizer["Role.SchoolOwner"], badges[0].TextContent.Trim());
        Assert.Equal(localizer["Role.Teacher"], badges[1].TextContent.Trim());
        Assert.Equal(localizer["Auth.Roles"], cut.Find(".role-badges").GetAttribute("aria-label"));
    }

    [Fact]
    public void Razor_components_have_no_unlocalized_literal_text_nodes()
    {
        var razorDirectory = Path.Combine(FindRepositoryRoot(), "src", "Asms.UI.Shared");
        var expression = new Regex(
            @"@\w+(?:\.\w+)*(?:\[[^\]]+\])?(?:\([^()]*\))?",
            RegexOptions.CultureInvariant);
        var controlStatement = new Regex(
            @"(?:@\s*(?:if|else|foreach|for|while|switch)\b|\}\s*else\b|\bcase\s+[^:]+:|\bbreak\s*;)[^<]*",
            RegexOptions.CultureInvariant);
        var literalLetters = new Regex(@"\p{L}", RegexOptions.CultureInvariant);
        var comments = new Regex(
            @"<!--[\s\S]*?-->|@\*[\s\S]*?\*@",
            RegexOptions.CultureInvariant);

        foreach (var file in Directory.EnumerateFiles(
                     razorDirectory,
                     "*.razor",
                     SearchOption.AllDirectories))
        {
            var source = comments.Replace(File.ReadAllText(file), string.Empty);
            var codeBlockStart = source.IndexOf("@code", StringComparison.Ordinal);
            if (codeBlockStart >= 0)
            {
                source = source[..codeBlockStart];
            }

            foreach (Match node in Regex.Matches(source, @">(?<text>[^<>]*)<"))
            {
                var text = controlStatement.Replace(node.Groups["text"].Value, string.Empty);
                text = expression.Replace(text, string.Empty);
                Assert.False(
                    literalLetters.IsMatch(text),
                    $"{Path.GetRelativePath(razorDirectory, file)} contains unlocalized text: {text}");
            }
        }
    }

    [Fact]
    public void Shared_css_uses_logical_properties_and_has_no_external_urls()
    {
        var cssPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Asms.UI.Shared",
            "wwwroot",
            "css",
            "asms.css");
        var css = File.ReadAllText(cssPath);
        var physicalProperties = new Regex(
            @"\b(?:margin|padding)-(?:left|right)\b|\btext-align\s*:\s*(?:left|right)\b|\bfloat\s*:\s*(?:left|right)\b|\bborder-(?:left|right)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.DoesNotMatch(physicalProperties, css);
        Assert.DoesNotContain("http", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cdn", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "font-family: \"Inter\", \"Noto Naskh Arabic\"",
            css,
            StringComparison.Ordinal);
    }

    private static LoginResponse CreateLoginResponse(IReadOnlyList<string> roles) =>
        new(
            Guid.NewGuid().ToString("N"),
            DateTime.UtcNow.AddMinutes(10),
            Guid.NewGuid(),
            Guid.NewGuid(),
            roles,
            Guid.NewGuid().ToString("N"),
            DateTime.UtcNow.AddDays(1));

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddAsmsUi();
        return context;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Asms.UI.Shared")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
