using Asms.Application.Abstractions;
using Asms.Application.Auth;
using Asms.Application.Security;
using Asms.Contracts.Auth;
using Asms.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Asms.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentTenant _tenant;
    private readonly IUserPermissionService _userPermissionService;

    public AuthController(
        IAuthService authService,
        ICurrentTenant tenant,
        IUserPermissionService userPermissionService)
    {
        _authService = authService;
        _tenant = tenant;
        _userPermissionService = userPermissionService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest? request,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (request is null ||
            string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.UserName.Length > 256 ||
            request.Password.Length > 256)
        {
            return BadRequest(new ErrorResponse("INVALID_REQUEST"));
        }

        var result = await _authService.LoginAsync(request.UserName, request.Password, ct);
        if (!result.Succeeded)
        {
            return Unauthorized(new ErrorResponse("INVALID_CREDENTIALS"));
        }

        if (result.AccessToken is null ||
            result.ExpiresAtUtc is not DateTime expiresAtUtc ||
            result.UserId is not Guid userId ||
            result.SchoolId is not Guid schoolId ||
            result.RefreshToken is null ||
            result.RefreshTokenExpiresAtUtc is not DateTime refreshTokenExpiresAtUtc)
        {
            throw new InvalidOperationException("The authentication service returned an invalid success result.");
        }

        return Ok(new LoginResponse(
            result.AccessToken,
            expiresAtUtc,
            userId,
            schoolId,
            result.Roles,
            result.RefreshToken,
            refreshTokenExpiresAtUtc));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest? request,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (request is null ||
            string.IsNullOrWhiteSpace(request.RefreshToken) ||
            request.RefreshToken.Length > 200)
        {
            return BadRequest(new ErrorResponse("INVALID_REQUEST"));
        }

        var result = await _authService.RefreshAsync(request.RefreshToken, ct);
        if (!result.Succeeded)
        {
            return Unauthorized(new ErrorResponse("INVALID_REFRESH_TOKEN"));
        }

        if (result.AccessToken is null ||
            result.ExpiresAtUtc is not DateTime expiresAtUtc ||
            result.UserId is not Guid userId ||
            result.SchoolId is not Guid schoolId ||
            result.RefreshToken is null ||
            result.RefreshTokenExpiresAtUtc is not DateTime refreshTokenExpiresAtUtc)
        {
            throw new InvalidOperationException("The authentication service returned an invalid success result.");
        }

        return Ok(new LoginResponse(
            result.AccessToken,
            expiresAtUtc,
            userId,
            schoolId,
            result.Roles,
            result.RefreshToken,
            refreshTokenExpiresAtUtc));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting("refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (request is not null &&
            !string.IsNullOrWhiteSpace(request.RefreshToken) &&
            request.RefreshToken.Length <= 200)
        {
            await _authService.LogoutAsync(request.RefreshToken, ct);
        }

        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (_tenant.UserId is Guid userId)
        {
            await _authService.RevokeAllForUserAsync(userId, "LOGOUT_ALL", ct);
        }

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        if (_tenant.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var roles = User.FindAll(AsmsClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return Ok(new
        {
            userId,
            schoolId = _tenant.SchoolId,
            roles
        });
    }

    [HttpGet("permissions")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Permissions(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var access = await _userPermissionService.GetAccessAsync(ct);
        if (!access.IsValid)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("FORBIDDEN"));
        }

        return Ok(new
        {
            roles = access.Roles.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
            permissions = access.Permissions.OrderBy(permission => permission, StringComparer.Ordinal).ToArray()
        });
    }
}
