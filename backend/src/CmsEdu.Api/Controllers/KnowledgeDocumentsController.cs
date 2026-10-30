using CmsEdu.Api.Authorization;
using CmsEdu.Application.Knowledge;
using CmsEdu.Domain.Authorization;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/knowledge-documents")]
[Authorize]
public class KnowledgeDocumentsController(IKnowledgeDocumentService documentService) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Curriculum.View)]
    public async Task<ActionResult<List<KnowledgeDocumentDto>>> GetList(
        [FromQuery] string? scopeGroup,
        [FromQuery] KnowledgeScope? scope,
        [FromQuery] int? courseId,
        [FromQuery] int? levelId,
        [FromQuery] int? classId,
        CancellationToken cancellationToken)
    {
        return Ok(await documentService.GetDocumentsAsync(scopeGroup, scope, courseId, levelId, classId, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.Curriculum.Manage)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<ActionResult<KnowledgeDocumentDto>> UploadDocument(
        [FromForm] IFormFile file,
        [FromForm] string? title,
        [FromForm] KnowledgeScope scope,
        [FromForm] int? courseId,
        [FromForm] int? levelId,
        [FromForm] int? classId,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Vui lòng chọn file tải lên.");

        await using var stream = file.OpenReadStream();
        var result = await documentService.UploadDocumentAsync(
            title,
            scope,
            courseId,
            levelId,
            classId,
            file.FileName,
            Path.GetExtension(file.FileName),
            file.Length,
            stream,
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Curriculum.Manage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await documentService.DeleteDocumentAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/pages/{page:int}/image")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPageImage(Guid id, int page, CancellationToken cancellationToken)
    {
        var stream = await documentService.GetPageImageAsync(id, page, cancellationToken);
        return File(stream, "image/png");
    }
}
