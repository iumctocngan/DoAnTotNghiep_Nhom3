using CmsEdu.Application.Authentication;

namespace CmsEdu.Application.Common.Interfaces;

public interface IAuthenticationService
{
    Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        string? refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task LogoutAllAsync(
        string userId,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
