using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Asms.Localization;
using Asms.Localization.Languages;
using Asms.Localization.Services;
using Asms.UI.Shared.Extensions;
using Asms.UI.Shared.Layout;
using Asms.UI.Shared.Pages;
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
    [InlineData("en")]
    [InlineData("ps")]
    [InlineData("fa-AF")]
    public void Home_navigation_text_matches_localized_resource_values(string languageCode)
    {
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<ILocalizer>();
        localizer.SetLanguage(languageCode);

        var cut = context.Render<Home>();
        var culture = CultureInfo.GetCultureInfo(languageCode);
        var resources = new ResourceManager(
            "Asms.Localization.Resources.SharedResource",
            typeof(SharedResource).Assembly);
        var keys = new[]
        {
            "Nav.Dashboard", "Nav.Students", "Nav.Teachers", "Nav.Attendance",
            "Nav.Exams", "Nav.Fees", "Nav.Reports", "Nav.Library",
            "Nav.Inventory", "Nav.Settings"
        };

        foreach (var key in keys)
        {
            var expected = resources.GetString(key, culture);
            Assert.False(string.IsNullOrEmpty(expected), $"{key} resource is missing.");
            Assert.Contains(expected, cut.Markup);
        }
    }

    [Fact]
    public void Home_back_button_returns_to_language_chooser()
    {
        using var context = CreateContext();
        var cut = context.Render<Home>();

        cut.Find("button[aria-label='Back']").Click();

        Assert.EndsWith(
            "/",
            context.Services.GetRequiredService<NavigationManager>().Uri,
            StringComparison.Ordinal);
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
    }

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
