using System.Text.Json;
using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Knowledge;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Services.Knowledge;

public class ClassChatService(
    AppDbContext dbContext,
    ICurrentUser currentUser,
    IRagModuleClient ragClient) : IClassChatService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<ClassKnowledgeInfoDto> GetClassKnowledgeInfoAsync(int classId, CancellationToken ct)
    {
        if (classId == 0)
        {
            if (!IsAdmin())
                throw new ForbiddenAccessException("Chỉ Quản trị viên mới có quyền tra cứu toàn bộ kho học liệu trung tâm.");

            var allDocs = await dbContext.Documents
                .AsNoTracking()
                .Where(d => d.Status == "ready")
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync(ct);

            var allDocDtos = allDocs.Select(d => new KnowledgeDocumentDto(
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
                d.CreatedAt)).ToList();

            return new ClassKnowledgeInfoDto(
                0,
                "Toàn bộ Trung tâm",
                "CENTER",
                "Tất cả cấp độ",
                "Kho Tri thức Tổng hợp",
                allDocDtos);
        }

        var cls = await EnsureClassAccessAsync(classId, ct);
        var courseId = cls.Level.CourseId;
        var levelId = cls.LevelId;

        var docs = await dbContext.Documents
            .AsNoTracking()
            .Include(d => d.Course)
            .Include(d => d.Level)
            .Include(d => d.Class)
            .Where(d => d.Status == "ready")
            .Where(d =>
                d.Scope == KnowledgeScope.Global ||
                (d.Scope == KnowledgeScope.Course && d.CourseId == courseId) ||
                (d.Scope == KnowledgeScope.Level && d.LevelId == levelId) ||
                (d.Scope == KnowledgeScope.Class && d.ClassId == classId))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        var docDtos = docs.Select(d => new KnowledgeDocumentDto(
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
            d.CreatedAt)).ToList();

        return new ClassKnowledgeInfoDto(
            cls.Id,
            cls.Name,
            cls.ClassCode,
            cls.Level.Name,
            cls.Level.Course.Name,
            docDtos);
    }

    public async Task<List<ClassMessageDto>> GetMessagesAsync(int classId, CancellationToken ct)
    {
        var userId = GetUserId();

        IQueryable<ClassChatMessage> query;
        if (classId == 0)
        {
            if (!IsAdmin())
                throw new ForbiddenAccessException("Chỉ Quản trị viên mới có quyền tra cứu toàn bộ kho học liệu trung tâm.");

            query = dbContext.ClassChatMessages
                .AsNoTracking()
                .Where(m => m.ClassId == null && m.UserId == userId);
        }
        else
        {
            await EnsureClassAccessAsync(classId, ct);
            query = dbContext.ClassChatMessages
                .AsNoTracking()
                .Where(m => m.ClassId == classId && m.UserId == userId);
        }

        return await query
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ClassMessageDto(
                m.Id,
                m.ClassId,
                m.Role,
                m.Content,
                DeserializeCitations(m.CitationsJson),
                m.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ClassChatResultDto> ChatAsync(int classId, ClassChatRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            throw new ValidationException("Câu hỏi không được để trống.");

        var userId = GetUserId();

        // 1. Verify access and determine available document IDs
        List<Guid> availableDocs;
        if (classId == 0)
        {
            if (!IsAdmin())
                throw new ForbiddenAccessException("Chỉ Quản trị viên mới có quyền tra cứu toàn bộ kho học liệu trung tâm.");

            availableDocs = await dbContext.Documents
                .AsNoTracking()
                .Where(d => d.Status == "ready")
                .Select(d => d.Id)
                .ToListAsync(ct);
        }
        else
        {
            var cls = await EnsureClassAccessAsync(classId, ct);
            var courseId = cls.Level.CourseId;
            var levelId = cls.LevelId;

            availableDocs = await dbContext.Documents
                .AsNoTracking()
                .Where(d => d.Status == "ready")
                .Where(d =>
                    d.Scope == KnowledgeScope.Global ||
                    (d.Scope == KnowledgeScope.Course && d.CourseId == courseId) ||
                    (d.Scope == KnowledgeScope.Level && d.LevelId == levelId) ||
                    (d.Scope == KnowledgeScope.Class && d.ClassId == classId))
                .Select(d => d.Id)
                .ToListAsync(ct);
        }

        var effectiveClassId = classId == 0 ? (int?)null : classId;

        if (availableDocs.Count == 0)
        {
            var noDocMsg = classId == 0
                ? "Kho tri thức trung tâm hiện chưa có tài liệu nào sẵn sàng. Vui lòng tải lên tài liệu trong phần Chương trình học."
                : "Chưa có tài liệu nào được phân phối cho lớp học này (khóa học, cấp độ, hoặc tài liệu chung). Vui lòng liên hệ Admin để tải lên tài liệu học liệu.";

            var userMsg = new ClassChatMessage
            {
                ClassId = effectiveClassId,
                UserId = userId,
                Role = "user",
                Content = request.Question.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            var botMsg = new ClassChatMessage
            {
                ClassId = effectiveClassId,
                UserId = userId,
                Role = "assistant",
                Content = noDocMsg,
                CreatedAt = DateTime.UtcNow.AddMilliseconds(50)
            };
            dbContext.ClassChatMessages.AddRange(userMsg, botMsg);
            await dbContext.SaveChangesAsync(ct);

            return new ClassChatResultDto(
                request.Question.Trim(),
                noDocMsg,
                [],
                DateTime.UtcNow);
        }

        // Filter by user selection if requested
        var targetDocIds = availableDocs;
        if (request.SelectedDocumentIds != null && request.SelectedDocumentIds.Count != 0)
        {
            var selectedSet = request.SelectedDocumentIds.ToHashSet();
            targetDocIds = availableDocs.Where(id => selectedSet.Contains(id)).ToList();
            if (targetDocIds.Count == 0)
                targetDocIds = availableDocs;
        }

        var sortedDocIds = targetDocIds.OrderBy(id => id).ToList();

        // 2. Query recent history for context
        var recentHistory = await dbContext.ClassChatMessages
            .AsNoTracking()
            .Where(m => m.ClassId == effectiveClassId && m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageItemDto(m.Role, m.Content))
            .ToListAsync(ct);

        var chatRequest = new ChatRequestDto(
            request.Question.Trim(),
            sortedDocIds,
            recentHistory);

        // 3. Query RAG Module (Module 2 performs Semantic Cache via Qdrant)
        var ragResponse = await ragClient.ScopedChatAsync(chatRequest, ct);

        var formattedCitations = ragResponse.Citations.Select(c => c with
        {
            ImageUrl = c.PageNumber.HasValue
                ? $"/api/knowledge-documents/{c.DocumentId}/pages/{c.PageNumber.Value}/image"
                : null
        }).ToList();

        var citationsJson = JsonSerializer.Serialize(formattedCitations, JsonOptions);

        var userMsgEntity = new ClassChatMessage
        {
            ClassId = effectiveClassId,
            UserId = userId,
            Role = "user",
            Content = request.Question.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var botMsgEntity = new ClassChatMessage
        {
            ClassId = effectiveClassId,
            UserId = userId,
            Role = "assistant",
            Content = ragResponse.Answer,
            CitationsJson = citationsJson,
            CreatedAt = DateTime.UtcNow.AddMilliseconds(50)
        };

        dbContext.ClassChatMessages.AddRange(userMsgEntity, botMsgEntity);
        await dbContext.SaveChangesAsync(ct);

        return new ClassChatResultDto(
            request.Question.Trim(),
            ragResponse.Answer,
            formattedCitations,
            DateTime.UtcNow);
    }

    public async Task ClearMessagesAsync(int classId, CancellationToken ct)
    {
        var userId = GetUserId();
        var effectiveClassId = classId == 0 ? (int?)null : classId;

        if (classId == 0 && !IsAdmin())
            throw new ForbiddenAccessException("Chỉ Quản trị viên mới có quyền thao tác trên kho học liệu trung tâm.");

        if (classId > 0)
            await EnsureClassAccessAsync(classId, ct);

        var messages = await dbContext.ClassChatMessages
            .Where(m => m.ClassId == effectiveClassId && m.UserId == userId)
            .ToListAsync(ct);

        if (messages.Count != 0)
        {
            dbContext.ClassChatMessages.RemoveRange(messages);
            await dbContext.SaveChangesAsync(ct);
        }
    }

    private async Task<Class> EnsureClassAccessAsync(int classId, CancellationToken ct)
    {
        var cls = await dbContext.Classes
            .AsNoTracking()
            .Include(c => c.Level)
            .ThenInclude(l => l.Course)
            .FirstOrDefaultAsync(c => c.Id == classId, ct)
            ?? throw new NotFoundException("Không tìm thấy thông tin lớp học.");

        if (!IsAdmin() && cls.MainTeacherUserId != GetUserId())
        {
            throw new ForbiddenAccessException("Bạn không có quyền truy cập học liệu và trợ lý của lớp học này.");
        }

        return cls;
    }

    private string GetUserId()
    {
        var id = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(id))
            throw new UnauthorizedAccessException("Người dùng chưa được xác thực.");
        return id;
    }

    private bool IsAdmin() => currentUser.Role == UserRole.Admin;

    private static List<CitationDto> DeserializeCitations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<CitationDto>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string GetScopeName(KnowledgeScope scope) => scope switch
    {
        KnowledgeScope.Global => "Tài liệu chung",
        KnowledgeScope.Course => "Chương trình học",
        KnowledgeScope.Level => "Chương trình học",
        KnowledgeScope.Class => "Lớp học",
        _ => "Không xác định"
    };
}
