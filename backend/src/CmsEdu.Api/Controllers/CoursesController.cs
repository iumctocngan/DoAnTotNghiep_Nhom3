using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Curriculum;
using CmsEdu.Api.Authorization;
using CmsEdu.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/courses")]
[Authorize]
[HasPermission(Permissions.Curriculum.View)]
public class CoursesController(CurriculumService curriculumService, AppDbContext duLieu) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CourseResponse>>> GetCourses(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await curriculumService.GetCoursesAsync(
            search, isActive, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CourseResponse>> GetCourse(
        int id,
        CancellationToken cancellationToken = default)
    {
        return Ok(await curriculumService.GetCourseAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.Curriculum.Manage)]
    public async Task<ActionResult<CourseResponse>> CreateCourse(
        CourseRequest request,
        CancellationToken cancellationToken = default)
    {
        request = request with { Code = await SinhMaTuDong.TaoAsync(duLieu, "KH", cancellationToken) };
        var course = await curriculumService.CreateCourseAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCourse), new { id = course.Id }, course);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Curriculum.Manage)]
    public async Task<ActionResult<CourseResponse>> UpdateCourse(
        int id,
        CourseRequest request,
        CancellationToken cancellationToken = default)
    {
        request = request with { Code = (await curriculumService.GetCourseAsync(id, cancellationToken)).Code };
        return Ok(await curriculumService.UpdateCourseAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/deactivate")]
    [HasPermission(Permissions.Curriculum.Manage)]
    public async Task<IActionResult> DeactivateCourse(
        int id,
        CancellationToken cancellationToken = default)
    {
        await curriculumService.DeactivateCourseAsync(id, cancellationToken);
        return NoContent();
    }
}
