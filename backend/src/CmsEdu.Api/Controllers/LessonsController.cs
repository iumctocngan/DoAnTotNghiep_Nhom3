using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Curriculum;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/lessons")]
[Authorize]
public class LessonsController(CurriculumService curriculumService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<LessonResponse>>> GetLessons(
        [FromQuery] int levelId,
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await curriculumService.GetLessonsAsync(
            levelId, search, isActive, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LessonResponse>> GetLesson(
        int id,
        CancellationToken cancellationToken = default)
    {
        return Ok(await curriculumService.GetLessonAsync(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<LessonResponse>> CreateLesson(
        LessonRequest request,
        CancellationToken cancellationToken = default)
    {
        var lesson = await curriculumService.CreateLessonAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetLesson), new { id = lesson.Id }, lesson);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<LessonResponse>> UpdateLesson(
        int id,
        LessonRequest request,
        CancellationToken cancellationToken = default)
    {
        return Ok(await curriculumService.UpdateLessonAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<IActionResult> DeactivateLesson(
        int id,
        CancellationToken cancellationToken = default)
    {
        await curriculumService.DeactivateLessonAsync(id, cancellationToken);
        return NoContent();
    }
}
