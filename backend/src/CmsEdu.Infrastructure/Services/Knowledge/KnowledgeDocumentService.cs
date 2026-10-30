using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Knowledge;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Services.Knowledge;

public class KnowledgeDocumentService(
    AppDbContext dbContext,
    ICurrentUser currentUser,
    IRagModuleClient ragClient) : IKnowledgeDocumentService
{
    private string GetUserId() =>
        currentUser.UserId ?? throw new ForbiddenAccessException("Không xác định được danh tính người dùng.");

    public async Task<List<KnowledgeDocumentDto>> GetDocumentsAsync(
        string? scopeGroup,
        KnowledgeScope? scope,
        int? courseId,
        int? levelId,
        int? classId,
        CancellationToken ct)
    {
        IQueryable<Document> query = dbContext.Documents
            .AsNoTracking()
            .Include(d => d.Course)
            .Include(d => d.Level)
            .Include(d => d.Class);

        // 1. Grouped / Specific Scope Filtering
        if (!string.IsNullOrWhiteSpace(scopeGroup))
        {
            var group = scopeGroup.Trim().ToLowerInvariant();
            if (group == "global")
                query = query.Where(d => d.Scope == KnowledgeScope.Global);
            else if (group == "curriculum")
                query = query.Where(d => d.Scope == KnowledgeScope.Course || d.Scope == KnowledgeScope.Level);
            else if (group == "class")
                query = query.Where(d => d.Scope == KnowledgeScope.Class);
        }
        else if (scope.HasValue)
        {
            query = query.Where(d => d.Scope == scope.Value);
        }

        if (courseId.HasValue)
            query = query.Where(d => d.CourseId == courseId.Value);

        if (levelId.HasValue)
            query = query.Where(d => d.LevelId == levelId.Value);

        if (classId.HasValue)
            query = query.Where(d => d.ClassId == classId.Value);

        // 2. Teacher Scoping (Option 1): Teachers only see documents related to classes they teach
        if (currentUser.Role == UserRole.Teacher && !string.IsNullOrEmpty(currentUser.UserId))
        {
            var teacherClasses = await dbContext.Classes
                .AsNoTracking()
                .Where(c => c.MainTeacherUserId == currentUser.UserId)
                .Select(c => new { c.Id, c.LevelId, CourseId = c.Level.CourseId })
                .ToListAsync(ct);

            var teacherClassIds = teacherClasses.Select(c => c.Id).Distinct().ToList();
            var teacherLevelIds = teacherClasses.Select(c => c.LevelId).Distinct().ToList();
            var teacherCourseIds = teacherClasses.Select(c => c.CourseId).Distinct().ToList();

            query = query.Where(d =>
                d.Scope == KnowledgeScope.Global ||
                (d.Scope == KnowledgeScope.Course && d.CourseId.HasValue && teacherCourseIds.Contains(d.CourseId.Value)) ||
                (d.Scope == KnowledgeScope.Level && d.LevelId.HasValue && teacherLevelIds.Contains(d.LevelId.Value)) ||
                (d.Scope == KnowledgeScope.Class && d.ClassId.HasValue && teacherClassIds.Contains(d.ClassId.Value))
            );
        }

        var list = await query.OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<KnowledgeDocumentDto> UploadDocumentAsync(
        string? title,
        KnowledgeScope scope,
        int? courseId,
        int? levelId,
        int? classId,
        string fileName,
        string extension,
        long fileSize,
        Stream fileStream,
        CancellationToken ct)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        if (ext is not ("pdf" or "docx"))
            throw new ValidationException("Hệ thống chỉ hỗ trợ định dạng PDF hoặc DOCX.");

        if (fileSize > 25 * 1024 * 1024)
            throw new ValidationException("Kích thước file không được vượt quá 25MB.");

        if (scope == KnowledgeScope.Course && !courseId.HasValue)
            throw new ValidationException("Vui lòng chọn Khóa học áp dụng cho tài liệu.");

        if (scope == KnowledgeScope.Level && !levelId.HasValue)
            throw new ValidationException("Vui lòng chọn Cấp độ áp dụng cho tài liệu.");

        if (scope == KnowledgeScope.Class && !classId.HasValue)
            throw new ValidationException("Vui lòng chọn Lớp học áp dụng cho tài liệu.");

        var docTitle = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(fileName) : title.Trim();

        var document = new Document
        {
            Title = docTitle,
            FileName = fileName,
            StoragePath = string.Empty,
            FileSize = fileSize,
            DocumentType = ext,
            Status = "processing",
            Scope = scope,
            CourseId = (scope == KnowledgeScope.Course || scope == KnowledgeScope.Level) ? courseId : null,
            LevelId = scope == KnowledgeScope.Level ? levelId : null,
            ClassId = scope == KnowledgeScope.Class ? classId : null,
            UploadedByUserId = GetUserId(),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(ct);

        try
        {
            var success = await ragClient.IngestKnowledgeDocumentAsync(document.Id, fileName, fileStream, ct);
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

        await dbContext.SaveChangesAsync(ct);

        if (document.CourseId.HasValue)
            await dbContext.Entry(document).Reference(d => d.Course).LoadAsync(ct);
        if (document.LevelId.HasValue)
            await dbContext.Entry(document).Reference(d => d.Level).LoadAsync(ct);
        if (document.ClassId.HasValue)
            await dbContext.Entry(document).Reference(d => d.Class).LoadAsync(ct);

        return ToDto(document);
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct)
    {
        var doc = await dbContext.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException("Không tìm thấy tài liệu.");

        dbContext.Documents.Remove(doc);

        // Invalidate answer caches citing this document or tied to its immediate scope
        var docIdStr = documentId.ToString();
        var docIdHex = documentId.ToString("N");
        var cachesToRemove = await dbContext.RagAnswerCaches
            .Where(c => (c.CitationsJson != null && (c.CitationsJson.Contains(docIdStr) || c.CitationsJson.Contains(docIdHex)))
                     || (doc.ClassId.HasValue && c.ClassId == doc.ClassId.Value)
                     || (doc.Scope == KnowledgeScope.Global && c.ClassId == 0))
            .ToListAsync(ct);

        if (cachesToRemove.Count != 0)
        {
            dbContext.RagAnswerCaches.RemoveRange(cachesToRemove);
        }

        await dbContext.SaveChangesAsync(ct);
        await ragClient.DeleteKnowledgeDocumentAsync(documentId, ct);
    }

    public async Task<Stream> GetPageImageAsync(Guid documentId, int page, CancellationToken ct)
    {
        var doc = await dbContext.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException("Không tìm thấy tài liệu.");

        return await ragClient.GetKnowledgeDocumentPageImageStreamAsync(documentId, page, ct);
    }

    private static KnowledgeDocumentDto ToDto(Document d) =>
        new(
            d.Id,
            d.Title,
            d.FileName,
            d.FileSize,
            d.DocumentType,
            d.Status,
            d.ErrorMessage,
            d.Scope,
            GetScopeName(d.Scope),
            d.CourseId,
            d.Course?.Name,
            d.LevelId,
            d.Level?.Name,
            d.ClassId,
            d.Class?.Name,
            d.UploadedByUserId,
            d.CreatedAt);

    private static string GetScopeName(KnowledgeScope scope) => scope switch
    {
        KnowledgeScope.Global => "Tài liệu chung",
        KnowledgeScope.Course => "Chương trình học",
        KnowledgeScope.Level => "Chương trình học",
        KnowledgeScope.Class => "Lớp học",
        _ => "Không xác định"
    };
}
