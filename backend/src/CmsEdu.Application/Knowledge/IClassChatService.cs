using CmsEdu.Application.Common.Interfaces;

namespace CmsEdu.Application.Knowledge;

public record ClassMessageDto(
    Guid Id,
    int? ClassId,
    string Role,
    string Content,
    List<CitationDto> Citations,
    DateTime CreatedAt);

public record ClassChatRequestDto(
    string Question,
    List<Guid>? SelectedDocumentIds = null);

public record ClassChatResultDto(
    string Question,
    string Answer,
    List<CitationDto> Citations,
    DateTime CreatedAt);

public record ClassKnowledgeInfoDto(
    int ClassId,
    string ClassName,
    string ClassCode,
    string LevelName,
    string CourseName,
    List<KnowledgeDocumentDto> Documents);

public interface IClassChatService
{
    Task<ClassKnowledgeInfoDto> GetClassKnowledgeInfoAsync(int classId, CancellationToken ct);
    Task<List<ClassMessageDto>> GetMessagesAsync(int classId, CancellationToken ct);
    Task<ClassChatResultDto> ChatAsync(int classId, ClassChatRequestDto request, CancellationToken ct);
    Task ClearMessagesAsync(int classId, CancellationToken ct);
}
