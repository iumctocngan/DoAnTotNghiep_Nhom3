namespace CmsEdu.Application.Authentication;

public sealed record CurrentUserResponse(
    string UserId,
    string EmployeeCode,
    string FullName,
    string Email,
    string Role);
