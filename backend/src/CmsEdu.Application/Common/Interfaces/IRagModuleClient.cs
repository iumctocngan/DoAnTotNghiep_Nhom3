namespace CmsEdu.Application.Common.Interfaces;

public record ChatMessageItemDto(string Role, string Content);

public record ChatRequestDto(
    string Question,
    List<Guid>? DocumentIds,
    List<ChatMessageItemDto>? History = null);

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

public interface IRagModuleClient
{
    Task<bool> IngestKnowledgeDocumentAsync(Guid documentId, string fileName, Stream fileStream, CancellationToken cancellationToken);
    Task<bool> DeleteKnowledgeDocumentAsync(Guid documentId, CancellationToken cancellationToken);
    Task<ChatResponseDto> ScopedChatAsync(ChatRequestDto request, CancellationToken cancellationToken);
    Task<Stream> GetKnowledgeDocumentPageImageStreamAsync(Guid documentId, int page, CancellationToken cancellationToken);
}
