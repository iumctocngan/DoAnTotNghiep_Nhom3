using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CmsEdu.Application.Authentication;
using CmsEdu.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IAuthenticationService authenticationService,
    IWebHostEnvironment environment) : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(request, GetIpAddress(), cancellationToken);
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);
        return Ok(result.Response);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(
        CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        }

        try
        {
            var result = await authenticationService.RefreshAsync(
                refreshToken,
                GetIpAddress(),
                cancellationToken);
            SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);
            return Ok(result.Response);
        }
        catch (UnauthorizedAccessException)
        {
            DeleteRefreshTokenCookie();
            throw;
        }
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken);
        await authenticationService.LogoutAsync(refreshToken, GetIpAddress(), cancellationToken);
        DeleteRefreshTokenCookie();
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userId is null)
        {
            return Unauthorized();
        }

        await authenticationService.LogoutAllAsync(userId, GetIpAddress(), cancellationToken);
        DeleteRefreshTokenCookie();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<CurrentUserResponse> Me()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var employeeCode = User.FindFirstValue("employee_code");
        var fullName = User.FindFirstValue(JwtRegisteredClaimNames.Name);
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email);
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (userId is null || employeeCode is null || fullName is null || email is null || role is null)
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserResponse(userId, employeeCode, fullName, email, role));
    }

    private string? GetIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private void SetRefreshTokenCookie(string refreshToken, DateTime expiresAtUtc)
    {
        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment() || Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAtUtc,
            Path = "/api/auth"
        });
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            Secure = !environment.IsDevelopment() || Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth"
        });
    }
}
