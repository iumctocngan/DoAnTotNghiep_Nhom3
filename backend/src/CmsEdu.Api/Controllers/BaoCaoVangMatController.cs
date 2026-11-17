using CmsEdu.Api.Authorization;
using CmsEdu.Application.AttendanceReports;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

/// <summary>Cung cấp báo cáo số buổi vắng theo học viên và khóa học.</summary>
[ApiController]
[Route("api/attendance-reports")]
[Authorize]
[HasPermission(Permissions.Sessions.Attendance)]
public sealed class AttendanceAbsenceReportsController(
    IAttendanceAbsenceReportService reportService) : ControllerBase
{
    /// <summary>
    /// Lấy báo cáo tổng hợp điểm danh vắng của các buổi đã hoàn tất, có thể lọc theo khóa học, lớp và ngày.
    /// </summary>
    [HttpGet("absences")]
    [ProducesResponseType(typeof(PagedResult<AttendanceAbsenceReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AttendanceAbsenceReportResponse>>> GetAbsences(
        [FromQuery] int? courseId,
        [FromQuery] int? classId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await reportService.GetAbsenceReportAsync(
            courseId, classId, fromDate, toDate, page, pageSize, cancellationToken));
    }
}
