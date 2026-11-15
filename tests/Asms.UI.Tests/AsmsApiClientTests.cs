using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Asms.Contracts.Auth;
using Asms.UI.Shared.Services;
using Microsoft.Extensions.Logging;

namespace Asms.UI.Tests;

public sealed class AsmsApiClientTests
{
    [Fact]
    public async Task Login_loads_permissions_and_signs_in_with_the_returned_tokens()
    {
        var response = CreateLoginResponse();
        AuthenticationHeaderValue? loginAuthorization = null;
        AuthenticationHeaderValue? permissionsAuthorization = null;
        using var harness = CreateHarness((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login", StringComparison.Ordinal))
            {
                loginAuthorization = request.Headers.Authorization;
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, response));
            }

            permissionsAuthorization = request.Headers.Authorization;
            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                new PermissionsResponse(["Teacher"], ["Students.View", "Attendance.View"])));
        });

        var result = await harness.Api.LoginAsync(
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"));

        Assert.True(result.Succeeded);
        Assert.Equal(response.AccessToken, harness.Session.AccessToken);
        Assert.Equal(response.RefreshToken, harness.Session.RefreshToken);
        Assert.True(harness.Session.Has("Students.View"));
        Assert.False(harness.Session.Has("Fees.View"));
        Assert.Null(loginAuthorization);
        Assert.Equal("Bearer", permissionsAuthorization?.Scheme);
        Assert.Equal(response.AccessToken, permissionsAuthorization?.Parameter);
    }

    [Fact]
    public async Task Invalid_credentials_return_server_code_without_signing_in()
    {
        using var harness = CreateHarness((_, _) =>
            Task.FromResult(JsonResponse(
                HttpStatusCode.Unauthorized,
                new ErrorResponse("INVALID_CREDENTIALS"))));

        var result = await harness.Api.LoginAsync(
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"));

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_CREDENTIALS", result.ErrorCode);
        Assert.False(harness.Session.IsSignedIn);
    }

    [Fact]
    public async Task Rate_limit_returns_rate_limited_error()
    {
        using var harness = CreateHarness((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        var result = await harness.Api.LoginAsync(
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"));

        Assert.False(result.Succeeded);
        Assert.Equal("RATE_LIMITED", result.ErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Network_errors_and_timeouts_return_network_error(bool timeout)
    {
        var provider = timeout ? new ImmediateTimeoutProvider() : TimeProvider.System;
        using var harness = CreateHarness(
            (_, cancellationToken) =>
            {
                if (!timeout)
                {
                    return Task.FromException<HttpResponseMessage>(new HttpRequestException());
                }

                var completion = new TaskCompletionSource<HttpResponseMessage>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
                return completion.Task;
            },
            provider);

        var result = await harness.Api.LoginAsync(
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"));

        Assert.False(result.Succeeded);
        Assert.Equal("NETWORK_ERROR", result.ErrorCode);
        Assert.True(result.IsNetworkError);
    }

    [Fact]
    public async Task Unreadable_success_body_returns_unexpected()
    {
        using var harness = CreateHarness((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json", Encoding.UTF8, "application/json")
            }));

        var result = await harness.Api.LoginAsync(
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"));

        Assert.False(result.Succeeded);
        Assert.Equal("Unexpected", result.ErrorCode);
        Assert.False(harness.Session.IsSignedIn);
    }

    [Fact]
    public async Task Authenticated_401_refreshes_once_and_retries_with_the_new_token()
    {
        var initial = CreateLoginResponse();
        var refreshed = CreateLoginResponse();
        var permissionCalls = 0;
        var refreshCalls = 0;
        var permissionTokens = new ConcurrentQueue<string?>();
        using var harness = CreateHarness(async (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref refreshCalls);
                return JsonResponse(HttpStatusCode.OK, refreshed);
            }

            permissionTokens.Enqueue(request.Headers.Authorization?.Parameter);
            if (Interlocked.Increment(ref permissionCalls) == 1)
            {
                return JsonResponse(
                    HttpStatusCode.Unauthorized,
                    new ErrorResponse("UNAUTHENTICATED"));
            }

            return JsonResponse(
                HttpStatusCode.OK,
                new PermissionsResponse(["Teacher"], ["Students.View"]));
        });
        harness.Session.SignIn(initial, ["Students.View"]);

        var result = await harness.Api.GetPermissionsAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(1, refreshCalls);
        Assert.Equal(2, permissionCalls);
        Assert.Equal([initial.AccessToken, refreshed.AccessToken], permissionTokens);
        Assert.Equal(refreshed.AccessToken, harness.Session.AccessToken);
    }

    [Fact]
    public async Task Failed_refresh_signs_out_and_returns_the_refresh_error()
    {
        var initial = CreateLoginResponse();
        var refreshCalls = 0;
        using var harness = CreateHarness((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref refreshCalls);
                return Task.FromResult(JsonResponse(
                    HttpStatusCode.Unauthorized,
                    new ErrorResponse("INVALID_REFRESH_TOKEN")));
            }

            return Task.FromResult(JsonResponse(
                HttpStatusCode.Unauthorized,
                new ErrorResponse("UNAUTHENTICATED")));
        });
        harness.Session.SignIn(initial, ["Students.View"]);

        var result = await harness.Api.GetPermissionsAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("INVALID_REFRESH_TOKEN", result.ErrorCode);
        Assert.Equal(1, refreshCalls);
        Assert.False(harness.Session.IsSignedIn);
        Assert.Null(harness.Session.AccessToken);
    }

    [Fact]
    public async Task Concurrent_unauthorized_calls_share_one_refresh()
    {
        var initial = CreateLoginResponse();
        var refreshed = CreateLoginResponse();
        var refreshCalls = 0;
        var permissionCalls = 0;
        var initialRequestsReady = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = CreateHarness(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/refresh", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref refreshCalls);
                await Task.Delay(50, cancellationToken);
                return JsonResponse(HttpStatusCode.OK, refreshed);
            }

            var count = Interlocked.Increment(ref permissionCalls);
            if (count <= 2)
            {
                if (count == 2)
                {
                    initialRequestsReady.TrySetResult(true);
                }

                await initialRequestsReady.Task.WaitAsync(cancellationToken);
                return JsonResponse(
                    HttpStatusCode.Unauthorized,
                    new ErrorResponse("UNAUTHENTICATED"));
            }

            return JsonResponse(
                HttpStatusCode.OK,
                new PermissionsResponse(["Teacher"], ["Students.View"]));
        });
        harness.Session.SignIn(initial, ["Students.View"]);

        var results = await Task.WhenAll(
            harness.Api.GetPermissionsAsync(),
            harness.Api.GetPermissionsAsync());

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(1, refreshCalls);
        Assert.Equal(4, permissionCalls);
    }

    [Fact]
    public async Task Permissions_request_has_no_authorization_header_when_signed_out()
    {
        var requestCount = 0;
        using var harness = CreateHarness((_, _) =>
        {
            Interlocked.Increment(ref requestCount);
            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                new PermissionsResponse([], [])));
        });

        var result = await harness.Api.GetPermissionsAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("UNAUTHENTICATED", result.ErrorCode);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task Logout_never_throws_and_clears_the_session_on_network_failure()
    {
        using var harness = CreateHarness((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException()));
        harness.Session.SignIn(CreateLoginResponse(), []);

        var exception = await Record.ExceptionAsync(harness.Api.LogoutAsync);

        Assert.Null(exception);
        Assert.False(harness.Session.IsSignedIn);
        Assert.Null(harness.Session.RefreshToken);
    }

    [Fact]
    public async Task Logs_never_contain_request_or_response_bodies()
    {
        var userName = Guid.NewGuid().ToString("N");
        var password = Guid.NewGuid().ToString("N");
        var responseText = Guid.NewGuid().ToString("N");
        using var harness = CreateHarness((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(responseText, Encoding.UTF8, "application/json")
            }));

        var result = await harness.Api.LoginAsync(userName, password);
        var output = string.Join(Environment.NewLine, harness.Logger.Messages);

        Assert.False(result.Succeeded);
        Assert.DoesNotContain(userName, output, StringComparison.Ordinal);
        Assert.DoesNotContain(password, output, StringComparison.Ordinal);
        Assert.DoesNotContain(responseText, output, StringComparison.Ordinal);
    }

    private static ApiHarness CreateHarness(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        TimeProvider? timeProvider = null) =>
        new(send, timeProvider ?? TimeProvider.System);

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, object body) =>
        new(statusCode)
        {
            Content = JsonContent.Create(body)
        };

    private static LoginResponse CreateLoginResponse() =>
        new(
            Guid.NewGuid().ToString("N"),
            DateTime.UtcNow.AddMinutes(10),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ["Teacher"],
            Guid.NewGuid().ToString("N"),
            DateTime.UtcNow.AddDays(1));

    private sealed class ApiHarness : IDisposable
    {
        private readonly HttpClient _httpClient;

        public ApiHarness(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
            TimeProvider timeProvider)
        {
            _httpClient = new HttpClient(new FakeHandler(send))
            {
                BaseAddress = new Uri("http://localhost:5082/"),
                Timeout = Timeout.InfiniteTimeSpan
            };
            Session = new AuthSession();
            Logger = new CapturingLogger<AsmsApiClient>();
            Api = new AsmsApiClient(_httpClient, Session, timeProvider, Logger);
        }

        public AuthSession Session { get; }

        public CapturingLogger<AsmsApiClient> Logger { get; }

        public AsmsApiClient Api { get; }

        public void Dispose() => _httpClient.Dispose();
    }

    private sealed class FakeHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Enqueue(formatter(state, exception));
        }
    }

    private sealed class ImmediateTimeoutProvider : TimeProvider
    {
        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                ThreadPool.QueueUserWorkItem(_ => callback(state));
            }

            return new NoOpTimer();
        }
    }

    private sealed class NoOpTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
