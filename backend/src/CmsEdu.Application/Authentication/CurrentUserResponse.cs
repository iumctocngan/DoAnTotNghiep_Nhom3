namespace CmsEdu.Application.Authentication;

public record CurrentUserResponse(
    string UserId,
    string EmployeeCode,
    string FullName,
    string Email,
    string Role);
