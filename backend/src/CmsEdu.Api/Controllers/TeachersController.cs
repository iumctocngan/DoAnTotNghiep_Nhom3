using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Teachers;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/teachers/{teacherId}")]
[Authorize(Roles = $"{UserRole.Admin},{UserRole.Teacher}")]
public class TeachersController(TeacherService teacherService) : ControllerBase
{
    [HttpGet("classes")]
    public async Task<ActionResult<PagedResult<TeacherClassResponse>>> GetClasses(
        string teacherId,
        [FromQuery] ClassStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await teacherService.GetClassesAsync(
            teacherId, status, page, pageSize, cancellationToken));
    }

    [HttpGet("schedule")]
    public async Task<ActionResult<PagedResult<TeacherScheduleResponse>>> GetSchedule(
        string teacherId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await teacherService.GetScheduleAsync(
            teacherId, fromDate, toDate, page, pageSize, cancellationToken));
    }
}
