using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Curriculum;
using CmsEdu.Api.Authorization;
using CmsEdu.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/levels")]
[Authorize]
[HasPermission(Permissions.Curriculum.View)]
public class LevelsController(CurriculumService curriculumService, AppDbContext duLieu) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<LevelResponse>>> GetLevels(
        [FromQuery] int courseId,
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await curriculumService.GetLevelsAsync(
            courseId, search, isActive, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LevelResponse>> GetLevel(
        int id,
        CancellationToken cancellationToken = default)
    {
        return Ok(await curriculumService.GetLevelAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.Curriculum.Manage)]
    public async Task<ActionResult<LevelResponse>> CreateLevel(
        LevelRequest request,
        CancellationToken cancellationToken = default)
    {
        request = request with { Code = await SinhMaTuDong.TaoAsync(duLieu, "CD", cancellationToken) };
        var level = await curriculumService.CreateLevelAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetLevel), new { id = level.Id }, level);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Curriculum.Manage)]
    public async Task<ActionResult<LevelResponse>> UpdateLevel(
        int id,
        LevelRequest request,
        CancellationToken cancellationToken = default)
    {
        request = request with { Code = (await curriculumService.GetLevelAsync(id, cancellationToken)).Code };
        return Ok(await curriculumService.UpdateLevelAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/deactivate")]
    [HasPermission(Permissions.Curriculum.Manage)]
    public async Task<IActionResult> DeactivateLevel(
        int id,
        CancellationToken cancellationToken = default)
    {
        await curriculumService.DeactivateLevelAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/activate")]
    [HasPermission(Permissions.Curriculum.Manage)]
    public async Task<IActionResult> ActivateLevel(
        int id,
        CancellationToken cancellationToken = default)
    {
        await curriculumService.ActivateLevelAsync(id, cancellationToken);
        return NoContent();
    }
}
