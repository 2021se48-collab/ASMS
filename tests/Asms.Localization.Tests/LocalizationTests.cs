using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Asms.Localization.Languages;
using Asms.Localization.Services;

namespace Asms.Localization.Tests;

public sealed class LocalizationTests
{
    private static readonly string[] ExpectedKeys =
    [
        "App.Name",
        "Common.Save", "Common.Cancel", "Common.Delete", "Common.Edit", "Common.Search",
        "Common.Add", "Common.Close", "Common.Yes", "Common.No", "Common.Confirm",
        "Common.Loading", "Common.Back", "Common.Next", "Common.Print", "Common.Export",
        "Nav.Dashboard", "Nav.Students", "Nav.Teachers", "Nav.Attendance", "Nav.Exams",
        "Nav.Fees", "Nav.Reports", "Nav.Library", "Nav.Inventory", "Nav.Settings",
        "Auth.Login", "Auth.Logout", "Auth.UserName", "Auth.Password", "Auth.ChooseLanguage",
        "Lang.English", "Lang.Pashto", "Lang.Dari",
        "Error.INVALID_CREDENTIALS", "Error.INVALID_REQUEST", "Error.INVALID_REFRESH_TOKEN",
        "Error.UNAUTHENTICATED", "Error.FORBIDDEN", "Error.APPEND_ONLY_TABLE", "Error.Unexpected",
        "Validation.Required", "Validation.MaxLength"
    ];

    private static readonly string[] ResourceFiles =
    [
        "SharedResource.resx",
        "SharedResource.ps.resx",
        "SharedResource.fa-AF.resx"
    ];

    [Fact]
    public void Every_required_key_has_a_value_in_all_three_languages()
    {
        var resources = ReadAllResources();

        foreach (var resource in resources.Values)
        {
            Assert.Equal(ExpectedKeys.OrderBy(key => key), resource.Keys.OrderBy(key => key));
            foreach (var key in ExpectedKeys)
            {
                Assert.False(string.IsNullOrWhiteSpace(resource[key]), $"{key} is empty.");
            }
        }
    }

    [Fact]
    public void Resource_files_have_the_same_keys_and_placeholder_numbers()
    {
        var resources = ReadAllResources();
        var english = resources["SharedResource.resx"];

        foreach (var resource in resources.Values)
        {
            Assert.Equal(english.Keys.OrderBy(key => key), resource.Keys.OrderBy(key => key));
            foreach (var key in ExpectedKeys)
            {
                Assert.Equal(
                    GetPlaceholders(english[key]),
                    GetPlaceholders(resource[key]));
            }
        }

        Assert.Equal(["0"], GetPlaceholders(english["Validation.Required"]));
        Assert.Equal(["0", "1"], GetPlaceholders(english["Validation.MaxLength"]));
    }

    [Fact]
    public void Pashto_and_Dari_values_are_translated_and_use_arabic_script()
    {
        var resources = ReadAllResources();
        var english = resources["SharedResource.resx"];
        var pashto = resources["SharedResource.ps.resx"];
        var dari = resources["SharedResource.fa-AF.resx"];
        var languageNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Lang.English",
            "Lang.Pashto",
            "Lang.Dari"
        };

        Assert.NotEqual(english["Common.Save"], pashto["Common.Save"]);
        Assert.NotEqual(english["Common.Save"], dari["Common.Save"]);
        Assert.Equal("English", english["Lang.English"]);
        Assert.Equal("انګلیسي", pashto["Lang.English"]);
        Assert.Equal("انگلیسی", dari["Lang.English"]);
        Assert.Equal("پښتو", pashto["Lang.Pashto"]);
        Assert.Equal("پښتو", dari["Lang.Pashto"]);
        Assert.Equal("دری", pashto["Lang.Dari"]);
        Assert.Equal("دری", dari["Lang.Dari"]);

        foreach (var key in ExpectedKeys)
        {
            Assert.True(
                languageNames.Contains(key) || GetPlaceholders(pashto[key]).Count > 0 ||
                ContainsArabicScript(pashto[key]),
                $"Pashto value for {key} has no Arabic-script characters.");
            Assert.True(
                languageNames.Contains(key) || GetPlaceholders(dari[key]).Count > 0 ||
                ContainsArabicScript(dari[key]),
                $"Dari value for {key} has no Arabic-script characters.");

            if (!languageNames.Contains(key))
            {
                Assert.True(
                    !string.Equals(english[key], pashto[key], StringComparison.OrdinalIgnoreCase),
                    $"Pashto value for {key} is unchanged from English.");
                Assert.True(
                    !string.Equals(english[key], dari[key], StringComparison.OrdinalIgnoreCase),
                    $"Dari value for {key} is unchanged from English.");
            }
        }
    }

    [Fact]
    public void Resource_values_have_no_edge_whitespace_or_todo_text()
    {
        foreach (var resource in ReadAllResources().Values)
        {
            foreach (var (key, value) in resource)
            {
                Assert.Equal(value, value.Trim());
                Assert.DoesNotContain("TODO", value, StringComparison.OrdinalIgnoreCase);
                Assert.False(string.IsNullOrWhiteSpace(key));
            }
        }
    }

    [Theory]
    [InlineData("en", "en", false)]
    [InlineData("EN", "en", false)]
    [InlineData("ps", "ps", true)]
    [InlineData("pa-AF", "ps", true)]
    [InlineData("ps-AF", "ps", true)]
    [InlineData("fa-AF", "fa-AF", true)]
    [InlineData("FA-af", "fa-AF", true)]
    [InlineData("fa", "fa-AF", true)]
    [InlineData("prs", "fa-AF", true)]
    [InlineData(null, "en", false)]
    [InlineData("", "en", false)]
    [InlineData("xx", "en", false)]
    public void Resolve_returns_supported_language_and_direction(
        string? code,
        string expectedCode,
        bool expectedRightToLeft)
    {
        var language = SupportedLanguages.Resolve(code);

        Assert.Equal(expectedCode, language.Code);
        Assert.Equal(expectedRightToLeft, language.IsRightToLeft);
    }

    [Fact]
    public void All_supported_languages_can_be_converted_to_culture()
    {
        foreach (var language in SupportedLanguages.All)
        {
            var culture = SupportedLanguages.ToCulture(language);
            Assert.NotNull(culture);
        }
    }

    [Fact]
    public void Localizer_reads_values_from_each_language_and_marks_missing_keys()
    {
        var localizer = new Localizer();

        Assert.Equal("Save", localizer["Common.Save"]);
        localizer.SetLanguage("ps");
        Assert.Equal("خوندي کول", localizer["Common.Save"]);
        Assert.Equal("rtl", localizer.Direction);
        localizer.SetLanguage("fa-AF");
        Assert.Equal("ذخیره", localizer["Common.Save"]);
        Assert.Equal("rtl", localizer.Direction);
        localizer.SetLanguage("en");
        Assert.Equal("ltr", localizer.Direction);
        Assert.Equal("[Unknown.Key]", localizer["Unknown.Key"]);
    }

    [Fact]
    public void Get_formats_values_and_returns_the_template_when_arguments_do_not_match()
    {
        var localizer = new Localizer();

        Assert.Equal("Student is required.", localizer.Get("Validation.Required", "Student"));
        Assert.Equal(
            "{0} must be at most {1} characters.",
            localizer.Get("Validation.MaxLength", "Name"));
    }

    [Fact]
    public void Missing_translation_falls_back_to_english()
    {
        var localizer = new Localizer(new EnglishOnlyResourceManager());
        localizer.SetLanguage("ps");

        Assert.Equal("English fallback", localizer["Test.EnglishOnly"]);
    }

    [Fact]
    public void Language_changed_fires_once_only_when_language_changes()
    {
        var localizer = new Localizer();
        var eventCount = 0;
        localizer.LanguageChanged += () => eventCount++;

        localizer.SetLanguage("en");
        localizer.SetLanguage("ps");
        localizer.SetLanguage("PS-AF");
        localizer.SetLanguage("fa-AF");

        Assert.Equal(2, eventCount);
        Assert.Equal(SupportedLanguages.Dari, localizer.CurrentLanguage);
    }

    [Fact]
    public void Concurrent_language_changes_do_not_throw()
    {
        var localizer = new Localizer();
        var codes = new[] { "en", "ps", "fa-AF", "prs", "pa-AF", "xx" };

        var exception = Record.Exception(() =>
            Parallel.For(0, 1000, index =>
            {
                localizer.SetLanguage(codes[index % codes.Length]);
                _ = localizer.CurrentLanguage;
                _ = localizer["Common.Save"];
            }));

        Assert.Null(exception);
    }

    private static Dictionary<string, Dictionary<string, string>> ReadAllResources()
    {
        var resourceDirectory = FindRepositoryRoot()
            .Combine("src", "Asms.Localization", "Resources");
        return ResourceFiles.ToDictionary(
            file => file,
            file => XDocument.Load(Path.Combine(resourceDirectory, file))
                .Root!
                .Elements("data")
                .ToDictionary(
                    element => (string)element.Attribute("name")!,
                    element => (string)element.Element("value")!,
                    StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Asms.Localization")))
            {
                return directory;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static List<string> GetPlaceholders(string value) =>
        Regex.Matches(value, @"(?<!\{)\{(\d+)\}(?!\})")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(index => index, StringComparer.Ordinal)
            .ToList();

    private static bool ContainsArabicScript(string value) =>
        value.Any(character => character is >= '\u0600' and <= '\u06FF');

    private sealed class EnglishOnlyResourceManager : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture)
        {
            return culture?.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true &&
                name == "Test.EnglishOnly"
                    ? "English fallback"
                    : null;
        }
    }
}

internal static class PathExtensions
{
    public static string Combine(this DirectoryInfo directory, params string[] paths) =>
        paths.Aggregate(directory.FullName, Path.Combine);
}
