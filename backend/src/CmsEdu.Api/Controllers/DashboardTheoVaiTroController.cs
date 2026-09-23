using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Dashboard;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardTheoVaiTroController(IDichVuDashboardVaiTro dichVu) : ControllerBase
{
    [HttpGet("admin")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<PhanHoiDashboardAdmin>> LayDashboardAdmin(
        CancellationToken cancellationToken = default)
    {
        return Ok(await dichVu.LayDashboardAdminAsync(cancellationToken));
    }

    [HttpGet("teacher")]
    [Authorize(Roles = UserRole.Teacher)]
    public async Task<ActionResult<PhanHoiDashboardTeacher>> LayDashboardTeacher(
        CancellationToken cancellationToken = default)
    {
        return Ok(await dichVu.LayDashboardTeacherAsync(cancellationToken));
    }

    [HttpGet("customer-care")]
    [Authorize(Roles = UserRole.CustomerCare)]
    public async Task<ActionResult<PhanHoiDashboardCustomerCare>> LayDashboardCustomerCare(
        CancellationToken cancellationToken = default)
    {
        return Ok(await dichVu.LayDashboardCustomerCareAsync(cancellationToken));
    }

    [HttpGet("accounting")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.Accountant}")]
    public async Task<ActionResult<PhanHoiDashboardTaiChinh>> LayDashboardKeToan(
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        CancellationToken cancellationToken = default)
    {
        return Ok(await dichVu.LayDashboardKeToanAsync(
            fromDate, toDate, cancellationToken));
    }
}
