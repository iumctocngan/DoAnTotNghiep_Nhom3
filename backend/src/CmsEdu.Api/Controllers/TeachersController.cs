using CmsEdu.Api.Authorization;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Teachers;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/teachers/{teacherId}")]
public class TeachersController(TeacherService teacherService) : ControllerBase
{
    [HttpGet("classes")]
    [Authorize(Policy = AuthorizationPolicies.TeacherClassesRead)]
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
    [Authorize(Policy = AuthorizationPolicies.TeacherScheduleRead)]
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
