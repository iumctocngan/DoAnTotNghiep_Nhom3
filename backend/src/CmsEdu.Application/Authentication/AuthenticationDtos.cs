namespace CmsEdu.Application.Authentication;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string UserId,
    string EmployeeCode,
    string FullName,
    string Email,
    string Role);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record CurrentUserResponse(
    string UserId,
    string EmployeeCode,
    string FullName,
    string Email,
    string Role);

public record AccessTokenResult(string Token, DateTime ExpiresAtUtc);

public record AuthenticationResult(
    LoginResponse Response,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
