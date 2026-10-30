using CmsEdu.Application.Common.Interfaces;

namespace CmsEdu.Application.Knowledge;

public record ClassConversationDto(
    Guid Id,
    int? ClassId,
    string Title,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MessageCount);

public record ClassMessageDto(
    Guid Id,
    Guid ConversationId,
    string Role,
    string Content,
    List<CitationDto> Citations,
    bool IsFromCache,
    DateTime CreatedAt);

public record ClassChatRequestDto(
    string Question,
    Guid? ConversationId = null,
    List<Guid>? SelectedDocumentIds = null);

public record ClassChatResultDto(
    Guid ConversationId,
    string Question,
    string Answer,
    List<CitationDto> Citations,
    bool IsFromCache,
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
    Task<List<ClassConversationDto>> GetConversationsAsync(int classId, CancellationToken ct);
    Task<List<ClassMessageDto>> GetConversationMessagesAsync(int classId, Guid conversationId, CancellationToken ct);
    Task<ClassConversationDto> CreateConversationAsync(int classId, string? title, CancellationToken ct);
    Task DeleteConversationAsync(int classId, Guid conversationId, CancellationToken ct);
    Task<ClassChatResultDto> ChatAsync(int classId, ClassChatRequestDto request, CancellationToken ct);
}
