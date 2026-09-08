namespace CmsEdu.Application.Authentication;

public sealed record AuthenticationResult(
    LoginResponse Response,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
