using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Dashboard;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

/// <summary>Cung cấp số liệu dashboard theo vai trò.</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardTaiChinhController(IDichVuDashboardTaiChinh dichVuDashboard) : ControllerBase
{
    /// <summary>Lấy doanh thu và công nợ hiện tại của dashboard kế toán.</summary>
    [HttpGet("accounting")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.Accountant}")]
    public async Task<ActionResult<PhanHoiDashboardTaiChinh>> LayDashboardKeToan(
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        CancellationToken cancellationToken = default)
    {
        return Ok(await dichVuDashboard.LayDashboardKeToanAsync(
            fromDate, toDate, cancellationToken));
    }
}
