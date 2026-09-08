namespace CmsEdu.Application.Authentication;

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string UserId,
    string EmployeeCode,
    string FullName,
    string Email,
    string Role);
