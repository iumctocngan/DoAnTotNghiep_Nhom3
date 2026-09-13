using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Staff;

public record StaffResponse(
    string Id,
    string EmployeeCode,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Role,
    EmploymentStatus Status);
