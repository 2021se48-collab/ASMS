using Asms.Application.Abstractions;
using Asms.Application.Auth;
using Asms.Application.Security;
using Asms.Contracts.Auth;
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

    public AuthController(IAuthService authService, ICurrentTenant tenant)
    {
        _authService = authService;
        _tenant = tenant;
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
            result.SchoolId is not Guid schoolId)
        {
            throw new InvalidOperationException("The authentication service returned an invalid success result.");
        }

        return Ok(new LoginResponse(
            result.AccessToken,
            expiresAtUtc,
            userId,
            schoolId,
            result.Roles));
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
}
