using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Curriculum;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/courses")]
[Authorize]
public class CoursesController(CurriculumService curriculumService) : ControllerBase
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
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<CourseResponse>> CreateCourse(
        CourseRequest request,
        CancellationToken cancellationToken = default)
    {
        var course = await curriculumService.CreateCourseAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCourse), new { id = course.Id }, course);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<CourseResponse>> UpdateCourse(
        int id,
        CourseRequest request,
        CancellationToken cancellationToken = default)
    {
        return Ok(await curriculumService.UpdateCourseAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<IActionResult> DeactivateCourse(
        int id,
        CancellationToken cancellationToken = default)
    {
        await curriculumService.DeactivateCourseAsync(id, cancellationToken);
        return NoContent();
    }
}
