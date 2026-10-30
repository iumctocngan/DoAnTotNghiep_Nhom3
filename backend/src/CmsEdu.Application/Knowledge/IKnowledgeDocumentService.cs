using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Knowledge;

public record KnowledgeDocumentDto(
    Guid Id,
    string Title,
    string FileName,
    long FileSize,
    string DocumentType,
    string Status,
    string? ErrorMessage,
    KnowledgeScope Scope,
    string ScopeName,
    int? CourseId,
    string? CourseName,
    int? LevelId,
    string? LevelName,
    int? ClassId,
    string? ClassName,
    string? UploadedByUserId,
    DateTime CreatedAt);

public record UploadKnowledgeDocumentDto(
    string? Title,
    KnowledgeScope Scope,
    int? CourseId,
    int? LevelId,
    int? ClassId);

public interface IKnowledgeDocumentService
{
    Task<List<KnowledgeDocumentDto>> GetDocumentsAsync(
        string? scopeGroup,
        KnowledgeScope? scope,
        int? courseId,
        int? levelId,
        int? classId,
        CancellationToken ct);

    Task<KnowledgeDocumentDto> UploadDocumentAsync(
        string? title,
        KnowledgeScope scope,
        int? courseId,
        int? levelId,
        int? classId,
        string fileName,
        string extension,
        long fileSize,
        Stream fileStream,
        CancellationToken ct);

    Task DeleteDocumentAsync(Guid id, CancellationToken ct);

    Task<Stream> GetPageImageAsync(Guid id, int page, CancellationToken ct);
}
