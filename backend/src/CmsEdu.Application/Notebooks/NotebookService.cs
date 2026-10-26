using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Entities;

namespace CmsEdu.Application.Notebooks;

public record NotebookSummaryDto(Guid Id, string Title, string? Description, DateTime CreatedAt, DateTime UpdatedAt, int DocumentCount);
public record DocumentItemDto(Guid Id, Guid NotebookId, string FileName, long FileSize, string DocumentType, string Status, string? ErrorMessage, DateTime CreatedAt);
public record NotebookDetailDto(Guid Id, string Title, string? Description, DateTime CreatedAt, DateTime UpdatedAt, List<DocumentItemDto> Documents);
public record CreateNotebookDto(string Title, string? Description);
public record UpdateNotebookDto(string Title, string? Description);

public class NotebookService(INotebookRepository repository, ICurrentUser currentUser, IRagModuleClient ragClient)
{
    private string GetTeacherId() =>
        currentUser.UserId ?? throw new ForbiddenAccessException("Không xác định được danh tính người dùng.");

    private async Task<Notebook> EnsureNotebookAsync(Guid id, CancellationToken ct) =>
        await repository.GetByIdAsync(id, GetTeacherId(), ct) ?? throw new NotFoundException("Không tìm thấy Notebook.");

    private async Task<Document> EnsureDocumentAsync(Guid notebookId, Guid documentId, CancellationToken ct) =>
        await repository.GetDocumentAsync(notebookId, documentId, GetTeacherId(), ct) ?? throw new NotFoundException("Không tìm thấy tài liệu.");

    public async Task<List<NotebookSummaryDto>> GetListAsync(CancellationToken ct)
    {
        var list = await repository.GetAllAsync(GetTeacherId(), ct);
        return list.Select(n => new NotebookSummaryDto(n.Id, n.Title, n.Description, n.CreatedAt, n.UpdatedAt, n.Documents.Count)).ToList();
    }

    public async Task<NotebookDetailDto> GetByIdAsync(Guid id, CancellationToken ct) =>
        ToDto(await EnsureNotebookAsync(id, ct));

    public async Task<NotebookDetailDto> CreateAsync(CreateNotebookDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new ValidationException("Tiêu đề Notebook không được để trống.");
        var notebook = new Notebook { Title = dto.Title.Trim(), Description = dto.Description?.Trim(), TeacherId = GetTeacherId() };
        await repository.AddNotebookAsync(notebook, ct);
        return ToDto(notebook);
    }

    public async Task<NotebookDetailDto> UpdateAsync(Guid id, UpdateNotebookDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new ValidationException("Tiêu đề Notebook không được để trống.");
        var notebook = await EnsureNotebookAsync(id, ct);
        notebook.Title = dto.Title.Trim();
        notebook.Description = dto.Description?.Trim();
        await repository.UpdateNotebookAsync(notebook, ct);
        return ToDto(notebook);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var notebook = await EnsureNotebookAsync(id, ct);
        await repository.DeleteNotebookAsync(notebook, ct);
        await ragClient.DeleteNotebookAsync(id, ct);
    }

    public async Task<DocumentItemDto> UploadDocumentAsync(Guid notebookId, string fileName, string extension, long fileSize, Stream fileStream, CancellationToken ct)
    {
        _ = await EnsureNotebookAsync(notebookId, ct);
        var ext = extension.TrimStart('.').ToLowerInvariant();
        if (ext is not ("pdf" or "docx")) throw new ValidationException("Hệ thống chỉ hỗ trợ định dạng PDF hoặc DOCX.");
        if (fileSize > 25 * 1024 * 1024) throw new ValidationException("Kích thước file không được vượt quá 25MB.");

        var document = new Document { NotebookId = notebookId, FileName = fileName, StoragePath = string.Empty, FileSize = fileSize, DocumentType = ext, Status = "processing" };
        await repository.AddDocumentAsync(document, ct);

        try
        {
            var success = await ragClient.IngestDocumentAsync(notebookId, document.Id, fileName, fileStream, ct);
            document.Status = success ? "ready" : "failed";
            document.ErrorMessage = success ? null : "Quá trình lập chỉ mục vector tại RAG Module thất bại.";
        }
        catch (HttpRequestException)
        {
            document.Status = "failed";
            document.ErrorMessage = "Không thể kết nối tới dịch vụ AI (RAG Module). Vui lòng đảm bảo dịch vụ AI đang chạy.";
        }
        catch (Exception ex)
        {
            document.Status = "failed";
            document.ErrorMessage = ex.Message;
        }

        await repository.UpdateDocumentAsync(document, ct);
        return ToDocumentDto(document);
    }

    public async Task DeleteDocumentAsync(Guid notebookId, Guid documentId, CancellationToken ct)
    {
        var document = await EnsureDocumentAsync(notebookId, documentId, ct);
        await repository.DeleteDocumentAsync(document, ct);
        await ragClient.DeleteDocumentAsync(notebookId, documentId, ct);
    }

    public async Task<ChatResponseDto> ChatAsync(Guid notebookId, ChatRequestDto request, CancellationToken ct)
    {
        _ = await EnsureNotebookAsync(notebookId, ct);
        var res = await ragClient.ChatAsync(notebookId, request, ct);
        var citations = res.Citations.Select(c => c with
        {
            ImageUrl = c.PageNumber.HasValue ? $"/api/notebooks/{notebookId}/documents/{c.DocumentId}/pages/{c.PageNumber.Value}/image" : null
        }).ToList();
        return res with { Citations = citations };
    }

    public async Task<Stream> GetPageImageAsync(Guid notebookId, Guid documentId, int page, CancellationToken ct)
    {
        _ = await EnsureDocumentAsync(notebookId, documentId, ct);
        return await ragClient.GetPageImageStreamAsync(notebookId, documentId, page, ct);
    }

    public async Task<List<DocumentOutlineItemDto>> GetDocumentOutlineAsync(Guid notebookId, Guid documentId, CancellationToken ct)
    {
        _ = await EnsureDocumentAsync(notebookId, documentId, ct);
        return await ragClient.GetDocumentOutlineAsync(notebookId, documentId, ct);
    }

    public async Task<(byte[] Bytes, string FileName)> GenerateCloRubricAsync(CloRubricRequestDto request, CancellationToken ct)
    {
        _ = await EnsureNotebookAsync(request.NotebookId, ct);
        return await ragClient.GenerateCloRubricAsync(request, ct);
    }

    private static NotebookDetailDto ToDto(Notebook n) =>
        new(n.Id, n.Title, n.Description, n.CreatedAt, n.UpdatedAt, n.Documents.OrderByDescending(d => d.CreatedAt).Select(ToDocumentDto).ToList());

    private static DocumentItemDto ToDocumentDto(Document d) =>
        new(d.Id, d.NotebookId, d.FileName, d.FileSize, d.DocumentType, d.Status, d.ErrorMessage, d.CreatedAt);
}
