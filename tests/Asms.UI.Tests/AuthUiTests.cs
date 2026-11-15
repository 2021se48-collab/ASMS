using Asms.Contracts.Auth;
using Asms.Localization.Services;
using Asms.UI.Shared.Extensions;
using Asms.UI.Shared.Layout;
using Asms.UI.Shared.Pages;
using Asms.UI.Shared.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Asms.UI.Tests;

public sealed class AuthUiTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ps")]
    [InlineData("fa-AF")]
    public void Login_renders_username_password_and_localized_sign_in_button(string languageCode)
    {
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<ILocalizer>();
        localizer.SetLanguage(languageCode);

        var cut = context.Bunit.Render<Login>();

        Assert.Equal("username", cut.Find("#login-user-name").GetAttribute("autocomplete"));
        Assert.Equal("password", cut.Find("#login-password").GetAttribute("type"));
        Assert.Equal("current-password", cut.Find("#login-password").GetAttribute("autocomplete"));
        Assert.Contains(localizer["Auth.SignIn"], cut.Find("button[type='submit']").TextContent);
    }

    [Fact]
    public async Task Login_empty_fields_show_localized_required_messages()
    {
        using var context = CreateContext();
        var localizer = context.Services.GetRequiredService<ILocalizer>();
        var cut = context.Bunit.Render<Login>();

        await SubmitAsync(cut);

        Assert.Contains(
            localizer.Get("Validation.Required", localizer["Auth.UserName"]),
            cut.Markup);
        Assert.Contains(
            localizer.Get("Validation.Required", localizer["Auth.Password"]),
            cut.Markup);
        Assert.Equal(0, context.Api.LoginCalls);
    }

    [Fact]
    public async Task Login_success_navigates_to_home()
    {
        using var context = CreateContext();
        context.Api.LoginResult = ApiResult<LoginResponse>.Success(CreateLoginResponse(["Teacher"]));
        var cut = context.Bunit.Render<Login>();
        cut.Find("#login-user-name").Change(Guid.NewGuid().ToString("N"));
        cut.Find("#login-password").Change(Guid.NewGuid().ToString("N"));

        await SubmitAsync(cut);

        Assert.EndsWith(
            "/home",
            context.Services.GetRequiredService<NavigationManager>().Uri,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_failure_shows_translated_alert_and_clears_password()
    {
        using var context = CreateContext();
        context.Api.LoginResult = ApiResult<LoginResponse>.Failure("INVALID_CREDENTIALS");
        var localizer = context.Services.GetRequiredService<ILocalizer>();
        var password = Guid.NewGuid().ToString("N");
        var cut = context.Bunit.Render<Login>();
        cut.Find("#login-user-name").Change(Guid.NewGuid().ToString("N"));
        cut.Find("#login-password").Change(password);

        await SubmitAsync(cut);

        Assert.Equal(localizer["Error.INVALID_CREDENTIALS"], cut.Find("[role='alert']").TextContent);
        Assert.Equal(string.Empty, cut.Find("#login-password").GetAttribute("value"));
    }

    [Fact]
    public async Task Login_button_is_disabled_while_request_is_pending()
    {
        using var context = CreateContext();
        var completion = new TaskCompletionSource<ApiResult<LoginResponse>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        context.Api.LoginHandler = (_, _) => completion.Task;
        var localizer = context.Services.GetRequiredService<ILocalizer>();
        var cut = context.Bunit.Render<Login>();
        cut.Find("#login-user-name").Change(Guid.NewGuid().ToString("N"));
        cut.Find("#login-password").Change(Guid.NewGuid().ToString("N"));

        var pendingSubmit = SubmitAsync(cut);
        cut.WaitForAssertion(() =>
        {
            var button = cut.Find("button[type='submit']");
            Assert.True(button.HasAttribute("disabled"));
            Assert.Contains(localizer["Auth.SigningIn"], button.TextContent);
        });

        completion.SetResult(ApiResult<LoginResponse>.Failure("INVALID_CREDENTIALS"));
        await pendingSubmit;
        Assert.False(cut.Find("button[type='submit']").HasAttribute("disabled"));
    }

    [Fact]
    public void Login_keeps_rtl_direction_and_language_switching_available()
    {
        using var context = CreateContext();
        context.Services.GetRequiredService<ILocalizer>().SetLanguage("ps");
        var cut = context.Bunit.Render<MainLayout>(parameters =>
            parameters.Add(layout => layout.Body, builder =>
            {
                builder.OpenComponent<Login>(0);
                builder.CloseComponent();
            }));

        Assert.Equal("rtl", cut.Find(".app-shell").GetAttribute("dir"));
        cut.Find("button[aria-label='انګلیسي']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("ltr", cut.Find(".app-shell").GetAttribute("dir"));
        });
    }

    [Fact]
    public void Teacher_sees_only_menu_items_allowed_by_permissions()
    {
        using var context = CreateSignedInContext(
            ["Teacher"],
            ["Students.View", "Attendance.View", "Exams.View"]);

        var cut = context.Bunit.Render<AppShell>();

        Assert.Contains("Students", cut.Markup);
        Assert.Contains("Attendance", cut.Markup);
        Assert.Contains("Exams", cut.Markup);
        Assert.DoesNotContain("Fees", cut.Markup);
        Assert.DoesNotContain("Settings", cut.Markup);
    }

    [Fact]
    public void Accountant_sees_fees_but_not_exams_without_their_permissions()
    {
        using var context = CreateSignedInContext(
            ["Accountant"],
            ["Fees.View", "Students.View"]);

        var cut = context.Bunit.Render<AppShell>();

        Assert.Contains("Fees", cut.Markup);
        Assert.Contains("Students", cut.Markup);
        Assert.DoesNotContain("Exams", cut.Markup);
    }

    [Fact]
    public void Parent_sees_no_staff_menu_even_with_staff_section_permissions()
    {
        using var context = CreateSignedInContext(
            ["Parent"],
            ["Students.View", "Attendance.View", "Exams.View", "Fees.View"]);

        var cut = context.Bunit.Render<AppShell>();

        Assert.Contains("Dashboard", cut.Markup);
        Assert.DoesNotContain("Students", cut.Markup);
        Assert.DoesNotContain("Attendance", cut.Markup);
        Assert.DoesNotContain("Exams", cut.Markup);
        Assert.DoesNotContain("Fees", cut.Markup);
        Assert.DoesNotContain("Settings", cut.Markup);
    }

    [Fact]
    public void Signed_out_user_is_redirected_from_app_shell_to_login()
    {
        using var context = CreateContext();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/home");

        _ = context.Bunit.Render<AppShell>();

        Assert.EndsWith(
            "/login",
            context.Services.GetRequiredService<NavigationManager>().Uri,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sign_out_clears_session_and_navigates_to_login()
    {
        using var context = CreateSignedInContext(["Teacher"], ["Students.View"]);
        var session = context.Services.GetRequiredService<IAuthSession>();
        var cut = context.Bunit.Render<AppShell>();

        await cut.Find(".sign-out-button").ClickAsync(new MouseEventArgs());

        Assert.False(session.IsSignedIn);
        Assert.EndsWith(
            "/login",
            context.Services.GetRequiredService<NavigationManager>().Uri,
            StringComparison.Ordinal);
    }

    [Fact]
    public void App_shell_language_and_theme_buttons_update_direction_and_theme()
    {
        using var context = CreateSignedInContext(["Teacher"], ["Students.View"]);
        var cut = context.Bunit.Render<AppShell>();

        cut.Find("button[aria-label='پښتو']").Click();
        cut.WaitForAssertion(() => Assert.Equal("rtl", cut.Find(".app-shell").GetAttribute("dir")));

        cut.Find(".theme-button").Click();
        cut.WaitForAssertion(() =>
            Assert.Equal("light", cut.Find(".app-shell").GetAttribute("data-theme")));
    }

    private static Task SubmitAsync(IRenderedComponent<Login> component) =>
        component.Find("form").TriggerEventAsync("onsubmit", new EventArgs());

    private static TestContext CreateContext()
    {
        var context = new TestContext();
        context.Bunit.Services.AddAsmsUi();
        context.Api = new FakeApiClient();
        context.Bunit.Services.RemoveAll<IAsmsApiClient>();
        context.Bunit.Services.AddSingleton<IAsmsApiClient>(context.Api);
        return context;
    }

    private static TestContext CreateSignedInContext(
        IReadOnlyList<string> roles,
        IEnumerable<string> permissions)
    {
        var context = CreateContext();
        context.Services.GetRequiredService<IAuthSession>()
            .SignIn(CreateLoginResponse(roles), permissions);
        return context;
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

    private sealed class TestContext : IDisposable
    {
        public BunitContext Bunit { get; } = new();

        public FakeApiClient Api { get; set; } = null!;

        public IServiceProvider Services => Bunit.Services;

        public void Dispose() => Bunit.Dispose();
    }

    private sealed class FakeApiClient : IAsmsApiClient
    {
        public int LoginCalls { get; private set; }

        public ApiResult<LoginResponse> LoginResult { get; set; } =
            ApiResult<LoginResponse>.Failure("INVALID_CREDENTIALS");

        public Func<string, string, Task<ApiResult<LoginResponse>>>? LoginHandler { get; set; }

        public Task<ApiResult<LoginResponse>> LoginAsync(string userName, string password)
        {
            LoginCalls++;
            return LoginHandler?.Invoke(userName, password) ?? Task.FromResult(LoginResult);
        }

        public Task<ApiResult<LoginResponse>> RefreshAsync() =>
            Task.FromResult(ApiResult<LoginResponse>.Failure("INVALID_REFRESH_TOKEN"));

        public Task LogoutAsync() => Task.CompletedTask;

        public Task<ApiResult<PermissionsResponse>> GetPermissionsAsync() =>
            Task.FromResult(ApiResult<PermissionsResponse>.Failure("UNAUTHENTICATED"));
    }
}
