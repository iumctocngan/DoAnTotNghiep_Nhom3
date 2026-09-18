using CmsEdu.Api.Authorization;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Curriculum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/levels")]
public class LevelsController(CurriculumService curriculumService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.CatalogRead)]
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
    [Authorize(Policy = AuthorizationPolicies.CatalogRead)]
    public async Task<ActionResult<LevelResponse>> GetLevel(
        int id,
        CancellationToken cancellationToken = default)
    {
        return Ok(await curriculumService.GetLevelAsync(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.CatalogManage)]
    public async Task<ActionResult<LevelResponse>> CreateLevel(
        LevelRequest request,
        CancellationToken cancellationToken = default)
    {
        var level = await curriculumService.CreateLevelAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetLevel), new { id = level.Id }, level);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthorizationPolicies.CatalogManage)]
    public async Task<ActionResult<LevelResponse>> UpdateLevel(
        int id,
        LevelRequest request,
        CancellationToken cancellationToken = default)
    {
        return Ok(await curriculumService.UpdateLevelAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = AuthorizationPolicies.CatalogManage)]
    public async Task<IActionResult> DeactivateLevel(
        int id,
        CancellationToken cancellationToken = default)
    {
        await curriculumService.DeactivateLevelAsync(id, cancellationToken);
        return NoContent();
    }
}
