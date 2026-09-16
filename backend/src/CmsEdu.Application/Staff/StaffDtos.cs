using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Staff;

public record CreateStaffRequest(
    string EmployeeCode,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Role,
    string TemporaryPassword);

public record UpdateStaffRequest(string FullName, string Email, string? PhoneNumber);

public record ChangeStaffRoleRequest(string Role);

public record ResetStaffPasswordRequest(string NewPassword);

public record StaffResponse(
    string Id,
    string EmployeeCode,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Role,
    EmploymentStatus Status);
