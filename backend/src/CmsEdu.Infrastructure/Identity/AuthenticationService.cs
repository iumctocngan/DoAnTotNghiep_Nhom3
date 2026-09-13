using System.Security.Cryptography;
using System.Text;
using CmsEdu.Application.Authentication;
using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CmsEdu.Infrastructure.Identity;

public class AuthenticationService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAccessTokenGenerator accessTokenGenerator,
    AppDbContext dbContext,
    IOptions<JwtOptions> options) : IAuthenticationService {
    private const string InvalidCredentialsMessage = "Email or password is invalid.";
    private const string InvalidRefreshTokenMessage = "Refresh token is invalid or expired.";
    private readonly JwtOptions _options = options.Value;

    public async Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password)) {
            throw new UnauthorizedAccessException(InvalidCredentialsMessage);
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.EmploymentStatus != EmploymentStatus.Active) {
            throw new UnauthorizedAccessException(InvalidCredentialsMessage);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (!signInResult.Succeeded) {
            throw new UnauthorizedAccessException(InvalidCredentialsMessage);
        }

        var role = await GetSingleRoleAsync(user);
        var (refreshToken, rawRefreshToken) = CreateRefreshToken(user.Id, Guid.NewGuid(), ipAddress);

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateAuthenticationResult(user, role, rawRefreshToken, refreshToken.ExpiresAt);
    }

    public async Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(refreshToken)) {
            throw new UnauthorizedAccessException(InvalidRefreshTokenMessage);
        }

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(refreshToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var storedToken = await dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null) {
            throw new UnauthorizedAccessException(InvalidRefreshTokenMessage);
        }

        if (storedToken.RevokedAt is not null || storedToken.ExpiresAt <= now) {
            await RevokeFamilyAsync(
                storedToken.UserId,
                storedToken.FamilyId,
                now,
                ipAddress,
                "Invalid or reused refresh token.",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new UnauthorizedAccessException(InvalidRefreshTokenMessage);
        }

        var user = await userManager.FindByIdAsync(storedToken.UserId);
        if (user is null || user.EmploymentStatus != EmploymentStatus.Active) {
            await RevokeFamilyAsync(
                storedToken.UserId,
                storedToken.FamilyId,
                now,
                ipAddress,
                "User account is inactive.",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new UnauthorizedAccessException(InvalidRefreshTokenMessage);
        }

        var role = await GetSingleRoleAsync(user);
        var (replacement, rawReplacement) = CreateRefreshToken(user.Id, storedToken.FamilyId, ipAddress);
        var updated = await dbContext.RefreshTokens
            .Where(token => token.Id == storedToken.Id && token.RevokedAt == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, now)
                .SetProperty(token => token.RevokedByIp, ipAddress)
                .SetProperty(token => token.ReplacedByTokenHash, replacement.TokenHash)
                .SetProperty(token => token.RevokeReason, "Rotated."), cancellationToken);

        if (updated != 1) {
            await RevokeFamilyAsync(
                storedToken.UserId,
                storedToken.FamilyId,
                now,
                ipAddress,
                "Refresh token reuse detected.",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new UnauthorizedAccessException(InvalidRefreshTokenMessage);
        }

        dbContext.RefreshTokens.Add(replacement);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CreateAuthenticationResult(user, role, rawReplacement, replacement.ExpiresAt);
    }

    public async Task LogoutAsync(
        string? refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(refreshToken)) {
            return;
        }

        var tokenHash = HashToken(refreshToken);
        var storedToken = await dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null) {
            return;
        }

        await RevokeFamilyAsync(
            storedToken.UserId,
            storedToken.FamilyId,
            DateTime.UtcNow,
            ipAddress,
            "Logged out.",
            cancellationToken);
    }

    public async Task LogoutAllAsync(
        string userId,
        string? ipAddress,
        CancellationToken cancellationToken = default) {
        await RevokeAllSessionsAsync(
            userId,
            ipAddress,
            "Logged out from all sessions.",
            cancellationToken);
    }

    public async Task RevokeAllSessionsAsync(
        string userId,
        string? ipAddress,
        string reason,
        CancellationToken cancellationToken = default) {
        await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, DateTime.UtcNow)
                .SetProperty(token => token.RevokedByIp, ipAddress)
                .SetProperty(token => token.RevokeReason, reason), cancellationToken);
    }

    public async Task ChangePasswordAsync(
        string userId,
        ChangePasswordRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword)) {
            throw new ValidationException("Current password and new password are required.");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null || user.EmploymentStatus != EmploymentStatus.Active) {
            throw new UnauthorizedAccessException("The account is unavailable.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) {
            throw new ValidationException(
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        await RevokeAllSessionsAsync(userId, ipAddress, "Password changed.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<string> GetSingleRoleAsync(ApplicationUser user) {
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count != 1 || !UserRole.AllRoles.Contains(roles[0])) {
            throw new UnauthorizedAccessException("The account does not have one valid role.");
        }

        return roles[0];
    }

    private AuthenticationResult CreateAuthenticationResult(
        ApplicationUser user,
        string role,
        string refreshToken,
        DateTime refreshTokenExpiresAt) {
        var accessToken = accessTokenGenerator.Generate(
            user.Id,
            user.EmployeeCode,
            user.FullName,
            user.Email!,
            role);

        return new AuthenticationResult(
            new LoginResponse(
                accessToken.Token,
                accessToken.ExpiresAtUtc,
                user.Id,
                user.EmployeeCode,
                user.FullName,
                user.Email!,
                role),
            refreshToken,
            refreshTokenExpiresAt);
    }

    private (RefreshToken Token, string RawToken) CreateRefreshToken(
        string userId,
        Guid familyId,
        string? ipAddress) {
        var now = DateTime.UtcNow;
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return (new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(rawToken),
            FamilyId = familyId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_options.RefreshTokenDays),
            CreatedByIp = ipAddress
        }, rawToken);
    }

    private async Task RevokeFamilyAsync(
        string userId,
        Guid familyId,
        DateTime revokedAt,
        string? ipAddress,
        string reason,
        CancellationToken cancellationToken) {
        await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedAt, revokedAt)
                .SetProperty(token => token.RevokedByIp, ipAddress)
                .SetProperty(token => token.RevokeReason, reason), cancellationToken);
    }

    private static string HashToken(string token) {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
