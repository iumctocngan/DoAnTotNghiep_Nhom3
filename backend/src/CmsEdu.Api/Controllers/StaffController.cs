using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Staff;
using CmsEdu.Api.Authorization;
using CmsEdu.Domain.Authorization;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/staff")]
[Authorize]
[HasPermission(Permissions.Staff.View)]
public class StaffController(IStaffService staffService, AppDbContext duLieu) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<StaffResponse>>> GetStaff(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] EmploymentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await staffService.GetStaffAsync(
            search, role, status, page, pageSize, cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StaffResponse>> GetStaffById(
        string id,
        CancellationToken cancellationToken)
    {
        return Ok(await staffService.GetStaffByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.Staff.Manage)]
    public async Task<ActionResult<StaffResponse>> CreateStaff(
        CreateStaffRequest request,
        CancellationToken cancellationToken)
    {
        request = request with { EmployeeCode = await SinhMaTuDong.TaoAsync(duLieu, SinhMaTuDong.TienToNhanVien(request.Role), cancellationToken) };
        var staff = await staffService.CreateStaffAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetStaffById), new { id = staff.Id }, staff);
    }

    [HttpPut("{id}")]
    [HasPermission(Permissions.Staff.Manage)]
    public async Task<ActionResult<StaffResponse>> UpdateStaff(
        string id,
        UpdateStaffRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await staffService.UpdateStaffAsync(id, request, cancellationToken));
    }

    [HttpPut("{id}/role")]
    [HasPermission(Permissions.Staff.Manage)]
    public async Task<IActionResult> ChangeRole(
        string id,
        ChangeStaffRoleRequest request,
        CancellationToken cancellationToken)
    {
        await staffService.ChangeRoleAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/reset-password")]
    [HasPermission(Permissions.Staff.Manage)]
    public async Task<IActionResult> ResetPassword(
        string id,
        ResetStaffPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await staffService.ResetPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/activate")]
    [HasPermission(Permissions.Staff.Manage)]
    public async Task<IActionResult> Activate(string id, CancellationToken cancellationToken)
    {
        await staffService.ActivateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/deactivate")]
    [HasPermission(Permissions.Staff.Manage)]
    public async Task<IActionResult> Deactivate(string id, CancellationToken cancellationToken)
    {
        await staffService.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
