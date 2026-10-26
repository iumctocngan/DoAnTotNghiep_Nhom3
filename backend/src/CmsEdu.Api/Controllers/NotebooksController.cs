using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Notebooks;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/notebooks")]
[Authorize(Roles = $"{UserRole.Admin},{UserRole.Teacher}")]
public class NotebooksController(NotebookService notebookService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NotebookSummaryDto>>> GetList(CancellationToken cancellationToken) =>
        Ok(await notebookService.GetListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotebookDetailDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await notebookService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<NotebookDetailDto>> Create(CreateNotebookDto dto, CancellationToken cancellationToken)
    {
        var notebook = await notebookService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = notebook.Id }, notebook);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NotebookDetailDto>> Update(Guid id, UpdateNotebookDto dto, CancellationToken cancellationToken) =>
        Ok(await notebookService.UpdateAsync(id, dto, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await notebookService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<ActionResult<DocumentItemDto>> UploadDocument(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Vui lòng chọn file tải lên.");

        await using var stream = file.OpenReadStream();
        var result = await notebookService.UploadDocumentAsync(
            id,
            file.FileName,
            Path.GetExtension(file.FileName),
            file.Length,
            stream,
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        await notebookService.DeleteDocumentAsync(id, documentId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/pages/{page:int}/image")]
    public async Task<IActionResult> GetPageImage(Guid id, Guid documentId, int page, CancellationToken cancellationToken)
    {
        var stream = await notebookService.GetPageImageAsync(id, documentId, page, cancellationToken);
        return File(stream, "image/png");
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}/outline")]
    public async Task<ActionResult<List<DocumentOutlineItemDto>>> GetDocumentOutline(Guid id, Guid documentId, CancellationToken cancellationToken) =>
        Ok(await notebookService.GetDocumentOutlineAsync(id, documentId, cancellationToken));


    [HttpPost("{id:guid}/chat")]
    public async Task<ActionResult<ChatResponseDto>> Chat(Guid id, ChatRequestDto request, CancellationToken cancellationToken) =>
        Ok(await notebookService.ChatAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/clo-rubric")]
    public async Task<IActionResult> GenerateCloRubric(Guid id, CloRubricRequestDto request, CancellationToken cancellationToken)
    {
        var (bytes, fileName) = await notebookService.GenerateCloRubricAsync(request with { NotebookId = id }, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
