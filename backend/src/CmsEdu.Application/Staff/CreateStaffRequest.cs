namespace CmsEdu.Application.Staff;

public record CreateStaffRequest(
    string EmployeeCode,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Role,
    string TemporaryPassword);
