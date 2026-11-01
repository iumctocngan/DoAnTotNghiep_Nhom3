using CmsEdu.Application.Common.Interfaces;

namespace CmsEdu.Application.Knowledge;

public record ClassConversationDto(
    Guid Id,
    int? ClassId,
    string Title,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MessageCount);

public record CreateConversationRequestDto(
    string? Title = null);

public record UpdateConversationTitleRequestDto(
    string Title);

public record ClassMessageDto(
    Guid Id,
    int? ClassId,
    Guid? ConversationId,
    string Role,
    string Content,
    List<CitationDto> Citations,
    DateTime CreatedAt);

public record ClassChatRequestDto(
    string Question,
    List<Guid>? SelectedDocumentIds = null,
    Guid? ConversationId = null);

public record ClassChatResultDto(
    string Question,
    string Answer,
    List<CitationDto> Citations,
    DateTime CreatedAt,
    Guid? ConversationId = null);

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
    Task<ClassConversationDto> CreateConversationAsync(int classId, CreateConversationRequestDto request, CancellationToken ct);
    Task<ClassConversationDto> UpdateConversationTitleAsync(int classId, Guid conversationId, UpdateConversationTitleRequestDto request, CancellationToken ct);
    Task DeleteConversationAsync(int classId, Guid conversationId, CancellationToken ct);
    Task<List<ClassMessageDto>> GetConversationMessagesAsync(int classId, Guid conversationId, CancellationToken ct);
    Task<List<ClassMessageDto>> GetMessagesAsync(int classId, CancellationToken ct);
    Task<ClassChatResultDto> ChatAsync(int classId, ClassChatRequestDto request, CancellationToken ct);
    Task ClearMessagesAsync(int classId, CancellationToken ct);
}
