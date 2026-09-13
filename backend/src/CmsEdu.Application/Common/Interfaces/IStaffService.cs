using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Staff;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Common.Interfaces;

public interface IStaffService
{
    Task<PagedResult<StaffResponse>> GetStaffAsync(
        string? search,
        string? role,
        EmploymentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<StaffResponse> GetStaffByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<StaffResponse> CreateStaffAsync(
        CreateStaffRequest request,
        CancellationToken cancellationToken = default);

    Task<StaffResponse> UpdateStaffAsync(
        string id,
        UpdateStaffRequest request,
        CancellationToken cancellationToken = default);

    Task ChangeRoleAsync(
        string id,
        ChangeStaffRoleRequest request,
        CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        string id,
        ResetStaffPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task ActivateAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        string id,
        CancellationToken cancellationToken = default);
}
