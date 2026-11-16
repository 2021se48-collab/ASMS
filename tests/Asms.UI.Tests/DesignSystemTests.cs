using System.Text.RegularExpressions;
using Asms.Localization.Services;
using Asms.UI.Shared.Components;
using Asms.UI.Shared.Extensions;
using Asms.UI.Shared.Layout;
using Asms.UI.Shared.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Asms.UI.Tests;

public sealed class DesignSystemTests
{
    [Theory]
    [InlineData("en", "ltr")]
    [InlineData("ps", "rtl")]
    [InlineData("fa-AF", "rtl")]
    public void Design_components_follow_the_active_language_and_direction(
        string languageCode,
        string expectedDirection)
    {
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<ILocalizer>();
        localizer.SetLanguage(languageCode);

        var cut = context.Render<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, builder =>
            {
                builder.OpenComponent<PageHeader>(0);
                builder.AddAttribute(1, nameof(PageHeader.TitleKey), "Nav.Dashboard");
                builder.CloseComponent();
            }));

        Assert.Equal(expectedDirection, cut.Find(".app-shell").GetAttribute("dir"));
        Assert.Contains(localizer["Nav.Dashboard"], cut.Find("h1").TextContent);
    }

    [Fact]
    public void Icon_is_accessible_only_when_a_label_is_supplied_and_directional_is_opt_in()
    {
        using var context = CreateContext();

        var decorative = context.Render<Icon>(parameters =>
            parameters.Add(icon => icon.Name, IconName.Chevron));
        var labeledDirectional = context.Render<Icon>(parameters => parameters
            .Add(icon => icon.Name, IconName.Chevron)
            .Add(icon => icon.Directional, true)
            .Add(icon => icon.Label, "Next"));

        Assert.Equal("true", decorative.Find("svg").GetAttribute("aria-hidden"));
        Assert.Null(decorative.Find("svg").GetAttribute("role"));
        Assert.DoesNotContain("asms-icon-directional", decorative.Find("svg").ClassName);
        Assert.Equal("img", labeledDirectional.Find("svg").GetAttribute("role"));
        Assert.Equal("Next", labeledDirectional.Find("svg").GetAttribute("aria-label"));
        Assert.Contains("asms-icon-directional", labeledDirectional.Find("svg").ClassName);

        var cssPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Asms.UI.Shared",
            "wwwroot",
            "css",
            "asms.css");
        var css = File.ReadAllText(cssPath);
        Assert.Contains("[dir=\"rtl\"] .asms-icon-directional", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Button_exposes_busy_disabled_and_click_behavior()
    {
        using var context = CreateContext();
        var clicked = false;

        var cut = context.Render<AsmsButton>(parameters => parameters
            .Add(button => button.Busy, true)
            .Add(button => button.OnClick, () => clicked = true)
            .AddChildContent("Save"));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-busy"));
        Assert.Single(cut.FindAll(".asms-button-spinner"));

        cut.Render(parameters => parameters
            .Add(button => button.Busy, false)
            .Add(button => button.OnClick, () => clicked = true)
            .AddChildContent("Save"));
        cut.Find("button").Click();

        Assert.True(clicked);
    }

    [Fact]
    public void Card_header_badge_empty_error_and_skeleton_render_their_states()
    {
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<ILocalizer>();
        var card = context.Render<AsmsCard>(parameters => parameters
            .Add(component => component.Interactive, true)
            .Add(component => component.AriaLabel, "Card")
            .AddChildContent("Content"));
        var header = context.Render<PageHeader>(parameters => parameters
            .Add(component => component.TitleKey, "Nav.Dashboard")
            .Add(component => component.Breadcrumbs, [new PageBreadcrumb("Nav.Home")]));
        var badge = context.Render<StatusBadge>(parameters => parameters
            .Add(component => component.Tone, StatusTone.Success)
            .AddChildContent("Active"));
        var emptyActionCount = 0;
        var empty = context.Render<EmptyState>(parameters => parameters
            .Add(component => component.MessageKey, "Common.NoResults")
            .Add(component => component.ActionLabelKey, "Common.Retry")
            .Add(component => component.OnAction, () => emptyActionCount++));
        var retryCount = 0;
        var error = context.Render<ErrorState>(parameters => parameters
            .Add(component => component.MessageKey, "Error.NETWORK_ERROR")
            .Add(component => component.OnRetry, () => retryCount++));
        var skeleton = context.Render<Skeleton>(parameters => parameters
            .Add(component => component.Kind, SkeletonKind.Table)
            .Add(component => component.Rows, 3));

        Assert.Contains("asms-card--interactive", card.Find("section").ClassName);
        Assert.Contains("Card", card.Find("section").GetAttribute("aria-label"));
        Assert.Contains(localizer["Nav.Dashboard"], header.Find("h1").TextContent);
        Assert.Contains(localizer["Common.Breadcrumbs"], header.Find("nav").GetAttribute("aria-label"));
        Assert.Contains("status-badge--success", badge.Find("span").ClassName);
        Assert.Contains(localizer["Common.NoResults"], empty.Markup);
        empty.Find("button").Click();
        Assert.Equal(1, emptyActionCount);
        Assert.Equal("alert", error.Find("section").GetAttribute("role"));
        error.Find("button").Click();
        Assert.Equal(1, retryCount);
        Assert.Equal(3, skeleton.FindAll(".asms-skeleton-row").Count);
    }

    [Fact]
    public void Toast_host_announces_and_dismisses_notifications()
    {
        using var context = CreateContext();
        var service = context.Services.GetRequiredService<IToastService>();
        var cut = context.Render<ToastHost>();

        service.ShowInfo("Common.Save", TimeSpan.FromSeconds(5));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("polite", cut.Find(".toast-host").GetAttribute("aria-live"));
            Assert.Contains(context.Services.GetRequiredService<ILocalizer>()["Common.Save"], cut.Markup);
        });
        cut.Find(".toast-message button").Click();

        Assert.Empty(cut.FindAll(".toast-message"));
        Assert.Empty(service.Notifications);
    }

    [Fact]
    public void Toast_service_auto_dismisses_notifications()
    {
        using var context = CreateContext();
        var service = context.Services.GetRequiredService<IToastService>();
        var cut = context.Render<ToastHost>();
        service.ShowSuccess("Common.Save", TimeSpan.FromMilliseconds(40));

        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll(".toast-message")),
            TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Confirm_dialog_traps_focus_handles_escape_and_restores_focus()
    {
        using var context = CreateContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = context.Render<ConfirmDialogTestHost>();
        var focusCalls = context.JSInterop.Invocations.Count;

        cut.Find(".focus-sentinel").TriggerEvent("onfocus", new FocusEventArgs());
        Assert.True(context.JSInterop.Invocations.Count > focusCalls);

        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, cut.Instance.CloseCount);
            Assert.False(cut.Instance.IsOpen);
            Assert.Empty(cut.FindAll("[role='dialog']"));
        });
        Assert.True(context.JSInterop.Invocations.Count > focusCalls + 1);
    }

    [Fact]
    public void Data_table_sorts_and_pages_rows()
    {
        using var context = CreateContext();
        var rows = new[]
        {
            new TestRow("Charlie"),
            new TestRow("Alpha"),
            new TestRow("Bravo")
        };
        var columns = new[]
        {
            new DataTableColumn<TestRow>("Nav.Students", row => row.Name, row => row.Name)
        };
        var cut = context.Render<DataTable<TestRow>>(parameters => parameters
            .Add(table => table.Items, rows)
            .Add(table => table.Columns, columns)
            .Add(table => table.PageSize, 2)
            .Add(table => table.SearchContent, builder => builder.AddContent(0, "search slot"))
            .Add(table => table.RowActions, row => builder => builder.AddContent(0, $"Edit {row.Name}")));

        Assert.Contains("Charlie", cut.Markup);
        Assert.Contains("Alpha", cut.Markup);
        Assert.Contains("search slot", cut.Find(".data-table-search-slot").TextContent);
        Assert.Contains("Edit Charlie", cut.Find("tbody").TextContent);
        Assert.Equal(
            context.Services.GetRequiredService<ILocalizer>()["Common.Actions"],
            cut.Find("thead th:last-child").GetAttribute("aria-label"));
        Assert.DoesNotContain("Bravo", cut.Find("tbody").TextContent);

        cut.Find(".data-table-pagination button:last-child").Click();
        Assert.Contains("Bravo", cut.Find("tbody").TextContent);
        cut.Find(".data-table-pagination button:first-child").Click();

        cut.Find(".data-table-sort").Click();
        var firstRow = cut.Find("tbody tr:first-child").TextContent;
        Assert.Contains("Alpha", firstRow);
        Assert.Equal("ascending", cut.Find("th").GetAttribute("aria-sort"));
    }

    [Fact]
    public void Data_table_renders_empty_loading_and_error_states()
    {
        using var context = CreateContext();
        var columns = new[] { new DataTableColumn<TestRow>("Nav.Students", row => row.Name) };
        var empty = context.Render<DataTable<TestRow>>(parameters => parameters
            .Add(table => table.Items, Array.Empty<TestRow>())
            .Add(table => table.Columns, columns));
        var loading = context.Render<DataTable<TestRow>>(parameters => parameters
            .Add(table => table.Items, Array.Empty<TestRow>())
            .Add(table => table.Columns, columns)
            .Add(table => table.IsLoading, true)
            .Add(table => table.LoadingRows, 2));
        var retries = 0;
        var error = context.Render<DataTable<TestRow>>(parameters => parameters
            .Add(table => table.Items, Array.Empty<TestRow>())
            .Add(table => table.Columns, columns)
            .Add(table => table.ErrorMessageKey, "Error.NETWORK_ERROR")
            .Add(table => table.OnRetry, () => retries++));

        Assert.Contains(context.Services.GetRequiredService<ILocalizer>()["Common.NoResults"], empty.Markup);
        Assert.Equal(2, loading.FindAll(".asms-skeleton-row").Count);
        Assert.Equal("alert", error.Find("[role='alert']").GetAttribute("role"));
        error.Find("button").Click();
        Assert.Equal(1, retries);
    }

    [Fact]
    public void Light_and_dark_text_pairs_meet_wcag_aa_contrast()
    {
        var cssPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Asms.UI.Shared",
            "wwwroot",
            "css",
            "asms.css");
        var css = File.ReadAllText(cssPath);
        var light = ExtractBlock(css, @":root");
        var dark = ExtractBlock(css, @"\[data-theme=""dark""\]");
        var semanticPairs = new[]
        {
            ("--color-text", "--color-background"),
            ("--color-text-muted", "--color-surface"),
            ("--color-text-muted-strong", "--color-background"),
            ("--color-text-muted-strong", "--color-surface-muted"),
            ("--color-on-primary", "--color-primary"),
            ("--color-on-primary", "--color-primary-hover"),
            ("--color-success-text", "--color-success-surface"),
            ("--color-warning-text", "--color-warning-surface"),
            ("--color-danger-text", "--color-danger-surface"),
            ("--color-info-text", "--color-info-surface")
        };

        AssertContrastPairs(light, semanticPairs);
        AssertContrastPairs(dark, semanticPairs);
    }

    private static void AssertContrastPairs(
        string block,
        IEnumerable<(string Foreground, string Background)> pairs)
    {
        foreach (var (foregroundToken, backgroundToken) in pairs)
        {
            var foreground = ReadColor(block, foregroundToken);
            var background = ReadColor(block, backgroundToken);
            Assert.True(
                ContrastRatio(foreground, background) >= 4.5,
                $"{foregroundToken} on {backgroundToken} does not meet 4.5:1 contrast.");
        }
    }

    private static string ExtractBlock(string css, string selector)
    {
        var match = Regex.Match(
            css,
            selector + @"\s*\{(?<body>[^}]*)\}",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Could not find CSS token block: {selector}");
        return match.Groups["body"].Value;
    }

    private static (int Red, int Green, int Blue) ReadColor(string block, string token)
    {
        var match = Regex.Match(
            block,
            Regex.Escape(token) + @"\s*:\s*#(?<hex>[0-9A-Fa-f]{6})",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Could not find color token: {token}");
        var value = match.Groups["hex"].Value;
        return (
            Convert.ToInt32(value.Substring(0, 2), 16),
            Convert.ToInt32(value.Substring(2, 2), 16),
            Convert.ToInt32(value.Substring(4, 2), 16));
    }

    private static double ContrastRatio(
        (int Red, int Green, int Blue) first,
        (int Red, int Green, int Blue) second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance((int Red, int Green, int Blue) color)
    {
        static double Linearize(int channel)
        {
            var value = channel / 255d;
            return value <= 0.04045
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linearize(color.Red) +
            0.7152 * Linearize(color.Green) +
            0.0722 * Linearize(color.Blue);
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

    private sealed record TestRow(string Name);

    private sealed class ConfirmDialogTestHost : ComponentBase
    {
        private ElementReference _returnFocusTo;

        public bool IsOpen { get; private set; } = true;

        public int CloseCount { get; private set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "type", "button");
            builder.AddElementReferenceCapture(2, element => _returnFocusTo = element);
            builder.CloseElement();

            builder.OpenComponent<ConfirmDialog>(3);
            builder.AddAttribute(4, nameof(ConfirmDialog.IsOpen), IsOpen);
            builder.AddAttribute(5, nameof(ConfirmDialog.TitleKey), "Common.Confirm");
            builder.AddAttribute(6, nameof(ConfirmDialog.MessageKey), "Common.Delete");
            builder.AddAttribute(7, nameof(ConfirmDialog.ReturnFocusTo), (ElementReference?)_returnFocusTo);
            builder.AddAttribute(8, nameof(ConfirmDialog.OnClose), EventCallback.Factory.Create(this, CountClose));
            builder.AddAttribute(
                9,
                nameof(ConfirmDialog.IsOpenChanged),
                EventCallback.Factory.Create<bool>(this, SetIsOpen));
            builder.CloseComponent();
        }

        private void CountClose() => CloseCount++;

        private void SetIsOpen(bool isOpen) => IsOpen = isOpen;
    }
}
