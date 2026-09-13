namespace CmsEdu.Application.Staff;

public record UpdateStaffRequest(
    string FullName,
    string Email,
    string? PhoneNumber);
