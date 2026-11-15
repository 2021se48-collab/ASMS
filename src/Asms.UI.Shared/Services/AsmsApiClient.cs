using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Asms.Contracts.Auth;
using Microsoft.Extensions.Logging;

namespace Asms.UI.Shared.Services;

public sealed class AsmsApiClient : IAsmsApiClient
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private readonly HttpClient _httpClient;
    private readonly IAuthSession _session;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AsmsApiClient> _logger;
    private readonly object _refreshGate = new();
    private Task<ApiResult<LoginResponse>>? _refreshTask;

    public AsmsApiClient(
        HttpClient httpClient,
        IAuthSession session,
        TimeProvider timeProvider,
        ILogger<AsmsApiClient> logger)
    {
        _httpClient = httpClient;
        _session = session;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ApiResult<LoginResponse>> LoginAsync(string userName, string password)
    {
        var loginCall = await SendJsonAsync<LoginResponse>(
            HttpMethod.Post,
            "api/auth/login",
            new LoginRequest(userName, password),
            accessToken: null);
        if (!loginCall.Result.Succeeded || loginCall.Result.Value is null)
        {
            return ApiResult<LoginResponse>.Failure(
                loginCall.Result.ErrorCode ?? "Unexpected",
                loginCall.Result.IsNetworkError);
        }

        var response = loginCall.Result.Value;
        if (!IsValidLoginResponse(response))
        {
            LogFailure("Unexpected");
            return ApiResult<LoginResponse>.Failure("Unexpected");
        }

        var permissionCall = await SendPermissionsWithTokenAsync(response.AccessToken);

        if (permissionCall.StatusCode == HttpStatusCode.Unauthorized)
        {
            var refreshCall = await SendJsonAsync<LoginResponse>(
                HttpMethod.Post,
                "api/auth/refresh",
                new RefreshRequest(response.RefreshToken),
                accessToken: null);
            if (!refreshCall.Result.Succeeded || refreshCall.Result.Value is null)
            {
                return ApiResult<LoginResponse>.Failure(
                    refreshCall.Result.ErrorCode ?? "Unexpected",
                    refreshCall.Result.IsNetworkError);
            }

            response = refreshCall.Result.Value;
            if (!IsValidLoginResponse(response))
            {
                LogFailure("Unexpected");
                return ApiResult<LoginResponse>.Failure("Unexpected");
            }

            permissionCall = await SendPermissionsWithTokenAsync(response.AccessToken);
        }

        if (!permissionCall.Result.Succeeded || permissionCall.Result.Value is null)
        {
            return ApiResult<LoginResponse>.Failure(
                permissionCall.Result.ErrorCode ?? "Unexpected",
                permissionCall.Result.IsNetworkError);
        }

        _session.SignIn(response, permissionCall.Result.Value.Permissions);
        return ApiResult<LoginResponse>.Success(response);
    }

    public async Task<ApiResult<LoginResponse>> RefreshAsync()
    {
        var task = GetOrStartRefresh(expectedAccessToken: null)!;
        var result = await task;
        if (!result.Succeeded)
        {
            _session.SignOut();
        }

        return result;
    }

    public async Task LogoutAsync()
    {
        var refreshToken = _session.RefreshToken;
        try
        {
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                _ = await SendJsonAsync<object>(
                    HttpMethod.Post,
                    "api/auth/logout",
                    new LogoutRequest(refreshToken),
                    _session.AccessToken,
                    allowNoContent: true);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Best-effort logout request failed ({ExceptionType}).",
                exception.GetType().Name);
        }
        finally
        {
            _session.SignOut();
        }
    }

    public async Task<ApiResult<PermissionsResponse>> GetPermissionsAsync()
    {
        var accessToken = _session.AccessToken;
        if (!_session.IsSignedIn || string.IsNullOrWhiteSpace(accessToken))
        {
            return ApiResult<PermissionsResponse>.Failure("UNAUTHENTICATED");
        }

        var call = await SendPermissionsAsync(accessToken);
        if (call.StatusCode != HttpStatusCode.Unauthorized)
        {
            return call.Result;
        }

        var refreshTask = GetOrStartRefresh(accessToken);
        if (refreshTask is not null)
        {
            var refresh = await refreshTask;
            if (!refresh.Succeeded)
            {
                _session.SignOut();
                return ApiResult<PermissionsResponse>.Failure(
                    refresh.ErrorCode ?? "UNAUTHENTICATED",
                    refresh.IsNetworkError);
            }
        }

        var currentAccessToken = _session.AccessToken;
        if (!_session.IsSignedIn || string.IsNullOrWhiteSpace(currentAccessToken))
        {
            _session.SignOut();
            return ApiResult<PermissionsResponse>.Failure("UNAUTHENTICATED");
        }

        call = await SendPermissionsAsync(currentAccessToken);
        if (call.StatusCode == HttpStatusCode.Unauthorized)
        {
            _session.SignOut();
        }

        return call.Result;
    }

    private Task<ApiCallResult<PermissionsResponse>> SendPermissionsAsync(string accessToken) =>
        SendPermissionsWithTokenAsync(accessToken);

    private async Task<ApiCallResult<PermissionsResponse>> SendPermissionsWithTokenAsync(string accessToken)
    {
        var call = await SendJsonAsync<PermissionsResponse>(
            HttpMethod.Get,
            "api/auth/permissions",
            body: null,
            accessToken);
        if (call.Result.Succeeded &&
            (call.Result.Value?.Roles is null || call.Result.Value.Permissions is null))
        {
            LogFailure("Unexpected");
            return new ApiCallResult<PermissionsResponse>(
                ApiResult<PermissionsResponse>.Failure("Unexpected"),
                call.StatusCode);
        }

        return call;
    }

    private static bool IsValidLoginResponse(LoginResponse response) =>
        !string.IsNullOrWhiteSpace(response.AccessToken) &&
        !string.IsNullOrWhiteSpace(response.RefreshToken) &&
        response.UserId != Guid.Empty &&
        response.SchoolId != Guid.Empty &&
        response.Roles is not null;

    private Task<ApiResult<LoginResponse>>? GetOrStartRefresh(string? expectedAccessToken)
    {
        lock (_refreshGate)
        {
            if (!_session.IsSignedIn || string.IsNullOrWhiteSpace(_session.RefreshToken))
            {
                return Task.FromResult(ApiResult<LoginResponse>.Failure("INVALID_REFRESH_TOKEN"));
            }

            if (expectedAccessToken is not null &&
                !string.Equals(_session.AccessToken, expectedAccessToken, StringComparison.Ordinal))
            {
                return null;
            }

            if (_refreshTask is { IsCompleted: false })
            {
                return _refreshTask;
            }

            _refreshTask = RefreshCoreAsync(_session.RefreshToken);
            return _refreshTask;
        }
    }

    private async Task<ApiResult<LoginResponse>> RefreshCoreAsync(string refreshToken)
    {
        var call = await SendJsonAsync<LoginResponse>(
            HttpMethod.Post,
            "api/auth/refresh",
            new RefreshRequest(refreshToken),
            accessToken: null);
        if (!call.Result.Succeeded || call.Result.Value is null)
        {
            return ApiResult<LoginResponse>.Failure(
                call.Result.ErrorCode ?? "Unexpected",
                call.Result.IsNetworkError);
        }

        if (!IsValidLoginResponse(call.Result.Value))
        {
            LogFailure("Unexpected");
            return ApiResult<LoginResponse>.Failure("Unexpected");
        }

        if (!_session.IsSignedIn)
        {
            return ApiResult<LoginResponse>.Failure("UNAUTHENTICATED");
        }

        _session.UpdateTokens(call.Result.Value);
        return call.Result;
    }

    private async Task<ApiCallResult<T>> SendJsonAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        string? accessToken,
        bool allowNoContent = false)
    {
        using var timeout = new CancellationTokenSource(RequestTimeout, _timeProvider);
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorCode = await ReadErrorCodeAsync(response, timeout.Token);
                LogFailure(errorCode);
                return new ApiCallResult<T>(
                    ApiResult<T>.Failure(errorCode),
                    response.StatusCode);
            }

            if (response.StatusCode == HttpStatusCode.NoContent && allowNoContent)
            {
                return new ApiCallResult<T>(
                    new ApiResult<T>(true, default, null, false),
                    response.StatusCode);
            }

            try
            {
                var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: timeout.Token);
                return value is null
                    ? new ApiCallResult<T>(ApiResult<T>.Failure("Unexpected"), response.StatusCode)
                    : new ApiCallResult<T>(ApiResult<T>.Success(value), response.StatusCode);
            }
            catch (JsonException)
            {
                LogFailure("Unexpected");
                return new ApiCallResult<T>(
                    ApiResult<T>.Failure("Unexpected"),
                    response.StatusCode);
            }
            catch (NotSupportedException)
            {
                LogFailure("Unexpected");
                return new ApiCallResult<T>(
                    ApiResult<T>.Failure("Unexpected"),
                    response.StatusCode);
            }
        }
        catch (HttpRequestException)
        {
            LogFailure("NETWORK_ERROR");
            return new ApiCallResult<T>(
                ApiResult<T>.Failure("NETWORK_ERROR", isNetworkError: true),
                null);
        }
        catch (OperationCanceledException)
        {
            LogFailure("NETWORK_ERROR");
            return new ApiCallResult<T>(
                ApiResult<T>.Failure("NETWORK_ERROR", isNetworkError: true),
                null);
        }
        catch (ObjectDisposedException)
        {
            LogFailure("Unexpected");
            return new ApiCallResult<T>(ApiResult<T>.Failure("Unexpected"), null);
        }
        catch (InvalidOperationException)
        {
            LogFailure("Unexpected");
            return new ApiCallResult<T>(ApiResult<T>.Failure("Unexpected"), null);
        }
    }

    private static async Task<string> ReadErrorCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return "RATE_LIMITED";
        }

        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(
                cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(error?.Code))
            {
                return error.Code;
            }
        }
        catch (JsonException)
        {
            return response.StatusCode == HttpStatusCode.Unauthorized
                ? "UNAUTHENTICATED"
                : "Unexpected";
        }
        catch (NotSupportedException)
        {
            return response.StatusCode == HttpStatusCode.Unauthorized
                ? "UNAUTHENTICATED"
                : "Unexpected";
        }

        return response.StatusCode == HttpStatusCode.Unauthorized
            ? "UNAUTHENTICATED"
            : "Unexpected";
    }

    private void LogFailure(string errorCode) =>
        _logger.LogWarning("ASMS API request failed ({ErrorCode}).", errorCode);

    private sealed record ApiCallResult<T>(ApiResult<T> Result, HttpStatusCode? StatusCode);
}
