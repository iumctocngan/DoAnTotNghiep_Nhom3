using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Dashboard;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class RoleDashboardController(IRoleDashboardService dashboardService) : ControllerBase
{
    [HttpGet("admin")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<AdminDashboardResponse>> GetAdminDashboard(
        CancellationToken cancellationToken = default)
    {
        return Ok(await dashboardService.GetAdminDashboardAsync(cancellationToken));
    }

    [HttpGet("teacher")]
    [Authorize(Roles = UserRole.Teacher)]
    public async Task<ActionResult<TeacherDashboardResponse>> GetTeacherDashboard(
        CancellationToken cancellationToken = default)
    {
        return Ok(await dashboardService.GetTeacherDashboardAsync(cancellationToken));
    }

    [HttpGet("customer-care")]
    [Authorize(Roles = UserRole.CustomerCare)]
    public async Task<ActionResult<CustomerCareDashboardResponse>> GetCustomerCareDashboard(
        CancellationToken cancellationToken = default)
    {
        return Ok(await dashboardService.GetCustomerCareDashboardAsync(cancellationToken));
    }

    [HttpGet("accounting")]
    [Authorize(Roles = UserRole.Accountant)]
    public async Task<ActionResult<AccountingDashboardResponse>> GetAccountingDashboard(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        return Ok(await dashboardService.GetAccountingDashboardAsync(
            fromDate, toDate, cancellationToken));
    }
}
