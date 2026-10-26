namespace CmsEdu.Application.Common.Interfaces;

public record ChatRequestDto(string Question, List<Guid>? DocumentIds);

public record CitationDto(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string Label,
    int? PageNumber,
    string? SectionTitle,
    double? Score,
    string? ImageUrl);

public record ChatResponseDto(string Answer, List<CitationDto> Citations);

public record CloRubricRequestDto(
    Guid NotebookId,
    Guid DocumentId,
    int? OutcomeCount,
    int? StartPage,
    int? EndPage);

public record DocumentOutlineItemDto(string Title, int StartPage, int EndPage);

public interface IRagModuleClient
{
    Task<bool> IngestDocumentAsync(Guid notebookId, Guid documentId, string fileName, Stream fileStream, CancellationToken cancellationToken);
    Task<bool> DeleteDocumentAsync(Guid notebookId, Guid documentId, CancellationToken cancellationToken);
    Task<bool> DeleteNotebookAsync(Guid notebookId, CancellationToken cancellationToken);
    Task<ChatResponseDto> ChatAsync(Guid notebookId, ChatRequestDto request, CancellationToken cancellationToken);
    Task<(byte[] Bytes, string FileName)> GenerateCloRubricAsync(CloRubricRequestDto request, CancellationToken cancellationToken);
    Task<Stream> GetPageImageStreamAsync(Guid notebookId, Guid documentId, int page, CancellationToken cancellationToken);
    Task<List<DocumentOutlineItemDto>> GetDocumentOutlineAsync(Guid notebookId, Guid documentId, CancellationToken cancellationToken);
}
