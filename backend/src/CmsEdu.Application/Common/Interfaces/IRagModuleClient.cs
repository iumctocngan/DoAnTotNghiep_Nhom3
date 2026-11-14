namespace CmsEdu.Application.Common.Interfaces;

public record ChatMessageItemDto(string Role, string Content);

public record ChatRequestDto(
    string Question,
    List<Guid>? DocumentIds,
    List<ChatMessageItemDto>? History = null,
    Guid? ConversationId = null,
    string? Summary = null,
    int SummaryProcessedCount = 0);

public record CitationDto(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string Label,
    int? PageNumber,
    string? SectionTitle,
    double? Score,
    string? ImageUrl);

public record ChatResponseDto(
    string Answer,
    List<CitationDto> Citations,
    string? NewSummary = null,
    int NewSummaryProcessedCount = 0);

public record CloRubricRequestDto(
    Guid DocumentId,
    int? OutcomeCount = 3,
    int? StartPage = null,
    int? EndPage = null);

public record DocumentOutlineItemDto(string Title, int StartPage, int EndPage);

public interface IRagModuleClient
{
    Task<bool> IngestKnowledgeDocumentAsync(Guid documentId, string fileName, Stream fileStream, CancellationToken cancellationToken);
    Task<bool> DeleteKnowledgeDocumentAsync(Guid documentId, CancellationToken cancellationToken);
    Task<ChatResponseDto> ScopedChatAsync(ChatRequestDto request, CancellationToken cancellationToken);
    Task<Stream> GetKnowledgeDocumentPageImageStreamAsync(Guid documentId, int page, CancellationToken cancellationToken);
    Task<(byte[] Bytes, string FileName)> GenerateCloRubricAsync(CloRubricRequestDto request, CancellationToken cancellationToken);
    Task<List<DocumentOutlineItemDto>> GetKnowledgeDocumentOutlineAsync(Guid documentId, CancellationToken cancellationToken);
}
