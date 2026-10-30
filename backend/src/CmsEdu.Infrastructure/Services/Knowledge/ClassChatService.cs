using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    private string GetUserId() =>
        currentUser.UserId ?? throw new ForbiddenAccessException("Không xác định được danh tính người dùng.");

    private bool IsAdmin() =>
        currentUser.Role == UserRole.Admin;

    private async Task<Class> EnsureClassAccessAsync(int classId, CancellationToken ct)
    {
        var cls = await dbContext.Classes
            .Include(c => c.Level)
            .ThenInclude(l => l.Course)
            .FirstOrDefaultAsync(c => c.Id == classId, ct)
            ?? throw new NotFoundException("Không tìm thấy thông tin lớp học.");

        if (!IsAdmin() && cls.MainTeacherUserId != GetUserId())
        {
            throw new ForbiddenAccessException("Bạn không có quyền truy cập kho học liệu của lớp học này.");
        }

        return cls;
    }

    public async Task<ClassKnowledgeInfoDto> GetClassKnowledgeInfoAsync(int classId, CancellationToken ct)
    {
        if (classId == 0)
        {
            if (!IsAdmin())
                throw new ForbiddenAccessException("Chỉ Quản trị viên mới có quyền tra cứu toàn bộ kho học liệu trung tâm.");

            var allDocs = await dbContext.Documents
                .AsNoTracking()
                .Include(d => d.Course)
                .Include(d => d.Level)
                .Include(d => d.Class)
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

    public async Task<List<ClassConversationDto>> GetConversationsAsync(int classId, CancellationToken ct)
    {
        var userId = GetUserId();

        IQueryable<ClassChatConversation> query;
        if (classId == 0)
        {
            if (!IsAdmin())
                throw new ForbiddenAccessException("Chỉ Quản trị viên mới có quyền tra cứu toàn bộ kho học liệu trung tâm.");

            query = dbContext.ClassChatConversations
                .AsNoTracking()
                .Where(c => c.ClassId == null && c.TeacherUserId == userId);
        }
        else
        {
            await EnsureClassAccessAsync(classId, ct);
            query = dbContext.ClassChatConversations
                .AsNoTracking()
                .Where(c => c.ClassId == classId);

            if (!IsAdmin())
                query = query.Where(c => c.TeacherUserId == userId);
        }

        var list = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new ClassConversationDto(
                c.Id,
                c.ClassId,
                c.Title,
                c.CreatedAt,
                c.UpdatedAt,
                c.Messages.Count))
            .ToListAsync(ct);

        return list;
    }

    public async Task<List<ClassMessageDto>> GetConversationMessagesAsync(int classId, Guid conversationId, CancellationToken ct)
    {
        var userId = GetUserId();

        ClassChatConversation conv;
        if (classId == 0)
        {
            if (!IsAdmin())
                throw new ForbiddenAccessException("Chỉ Quản trị viên mới có quyền tra cứu toàn bộ kho học liệu trung tâm.");

            conv = await dbContext.ClassChatConversations
                .AsNoTracking()
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.ClassId == null, ct)
                ?? throw new NotFoundException("Không tìm thấy phiên trò chuyện.");
        }
        else
        {
            await EnsureClassAccessAsync(classId, ct);
            conv = await dbContext.ClassChatConversations
                .AsNoTracking()
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.ClassId == classId, ct)
                ?? throw new NotFoundException("Không tìm thấy phiên trò chuyện.");
        }

        if (!IsAdmin() && conv.TeacherUserId != userId)
            throw new ForbiddenAccessException("Bạn không có quyền xem phiên trò chuyện này.");

        return conv.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ClassMessageDto(
                m.Id,
                m.ConversationId,
                m.Role,
                m.Content,
                DeserializeCitations(m.CitationsJson),
                m.IsFromCache,
                m.CreatedAt))
            .ToList();
    }

    public async Task<ClassConversationDto> CreateConversationAsync(int classId, string? title, CancellationToken ct)
    {
        if (classId == 0)
        {
            if (!IsAdmin())
                throw new ForbiddenAccessException("Chỉ Quản trị viên mới có quyền tạo phiên trò chuyện toàn trung tâm.");
        }
        else
        {
            await EnsureClassAccessAsync(classId, ct);
        }

        var conv = new ClassChatConversation
        {
            ClassId = classId == 0 ? null : classId,
            TeacherUserId = GetUserId(),
            Title = string.IsNullOrWhiteSpace(title) ? $"Phiên hỏi đáp {DateTime.Now:dd/MM/yyyy HH:mm}" : title.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.ClassChatConversations.Add(conv);
        await dbContext.SaveChangesAsync(ct);

        return new ClassConversationDto(conv.Id, conv.ClassId, conv.Title, conv.CreatedAt, conv.UpdatedAt, 0);
    }

    public async Task DeleteConversationAsync(int classId, Guid conversationId, CancellationToken ct)
    {
        var userId = GetUserId();

        ClassChatConversation conv;
        if (classId == 0)
        {
            if (!IsAdmin())
                throw new ForbiddenAccessException("Chỉ Quản trị viên mới có quyền xóa phiên trò chuyện này.");

            conv = await dbContext.ClassChatConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.ClassId == null, ct)
                ?? throw new NotFoundException("Không tìm thấy phiên trò chuyện.");
        }
        else
        {
            await EnsureClassAccessAsync(classId, ct);
            conv = await dbContext.ClassChatConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.ClassId == classId, ct)
                ?? throw new NotFoundException("Không tìm thấy phiên trò chuyện.");
        }

        if (!IsAdmin() && conv.TeacherUserId != userId)
            throw new ForbiddenAccessException("Bạn không có quyền xóa phiên trò chuyện này.");

        dbContext.ClassChatConversations.Remove(conv);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<ClassChatResultDto> ChatAsync(int classId, ClassChatRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            throw new ValidationException("Câu hỏi không được để trống.");

        var userId = GetUserId();

        // 1. Get or create conversation
        ClassChatConversation conversation;
        if (classId == 0)
        {
            if (!IsAdmin())
                throw new ForbiddenAccessException("Chỉ Quản trị viên mới có quyền tra cứu toàn bộ kho học liệu trung tâm.");

            if (request.ConversationId.HasValue)
            {
                conversation = await dbContext.ClassChatConversations
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value && c.ClassId == null, ct)
                    ?? throw new NotFoundException("Không tìm thấy phiên trò chuyện.");

                if (!IsAdmin() && conversation.TeacherUserId != userId)
                    throw new ForbiddenAccessException("Bạn không có quyền sử dụng phiên trò chuyện này.");
            }
            else
            {
                var autoTitle = request.Question.Length > 40 ? request.Question[..37] + "..." : request.Question;
                conversation = new ClassChatConversation
                {
                    ClassId = null,
                    TeacherUserId = userId,
                    Title = autoTitle,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.ClassChatConversations.Add(conversation);
                await dbContext.SaveChangesAsync(ct);
            }
        }
        else
        {
            await EnsureClassAccessAsync(classId, ct);
            if (request.ConversationId.HasValue)
            {
                conversation = await dbContext.ClassChatConversations
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => c.Id == request.ConversationId.Value && c.ClassId == classId, ct)
                    ?? throw new NotFoundException("Không tìm thấy phiên trò chuyện.");

                if (!IsAdmin() && conversation.TeacherUserId != userId)
                    throw new ForbiddenAccessException("Bạn không có quyền sử dụng phiên trò chuyện này.");
            }
            else
            {
                var autoTitle = request.Question.Length > 40 ? request.Question[..37] + "..." : request.Question;
                conversation = new ClassChatConversation
                {
                    ClassId = classId,
                    TeacherUserId = userId,
                    Title = autoTitle,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.ClassChatConversations.Add(conversation);
                await dbContext.SaveChangesAsync(ct);
            }
        }

        // 2. Determine allowed document IDs
        List<Guid> availableDocs;
        if (classId == 0)
        {
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

        if (availableDocs.Count == 0)
        {
            var noDocMsg = classId == 0
                ? "Kho tri thức trung tâm hiện chưa có tài liệu nào sẵn sàng. Vui lòng tải lên tài liệu trong phần Chương trình học."
                : "Chưa có tài liệu nào được phân phối cho lớp học này (khóa học, cấp độ, hoặc tài liệu chung). Vui lòng liên hệ Admin để tải lên tài liệu học liệu.";
            var userMsg = new ClassChatMessage
            {
                ConversationId = conversation.Id,
                Role = "user",
                Content = request.Question.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            var botMsg = new ClassChatMessage
            {
                ConversationId = conversation.Id,
                Role = "assistant",
                Content = noDocMsg,
                IsFromCache = false,
                CreatedAt = DateTime.UtcNow.AddMilliseconds(100)
            };
            dbContext.ClassChatMessages.AddRange(userMsg, botMsg);
            conversation.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            return new ClassChatResultDto(
                conversation.Id,
                request.Question.Trim(),
                noDocMsg,
                [],
                false,
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
        var normalizedQuestion = NormalizeQuestion(request.Question);

        // 3. Compute Cache Key: SHA256(ClassId + ":" + SortedDocIds + ":" + NormalizedQuestion)
        var docIdsFingerprint = string.Join(",", sortedDocIds.Select(id => id.ToString("N")));
        var rawCacheKey = $"{classId}:{docIdsFingerprint}:{normalizedQuestion}";
        var cacheKey = ComputeSha256(rawCacheKey);

        // 4. Check Answer Cache
        var cachedEntry = await dbContext.RagAnswerCaches
            .FirstOrDefaultAsync(c => c.CacheKey == cacheKey, ct);

        if (cachedEntry != null)
        {
            cachedEntry.HitCount++;
            cachedEntry.LastAccessedAt = DateTime.UtcNow;

            var cachedCitations = DeserializeCitations(cachedEntry.CitationsJson);

            // Persist conversation messages
            var userMessage = new ClassChatMessage
            {
                ConversationId = conversation.Id,
                Role = "user",
                Content = request.Question.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            var assistantMessage = new ClassChatMessage
            {
                ConversationId = conversation.Id,
                Role = "assistant",
                Content = cachedEntry.Answer,
                CitationsJson = cachedEntry.CitationsJson,
                IsFromCache = true,
                CreatedAt = DateTime.UtcNow.AddMilliseconds(50)
            };

            dbContext.ClassChatMessages.AddRange(userMessage, assistantMessage);
            conversation.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            return new ClassChatResultDto(
                conversation.Id,
                request.Question.Trim(),
                cachedEntry.Answer,
                cachedCitations,
                true,
                DateTime.UtcNow);
        }

        // 5. Cache Miss -> Query RAG Module
        var recentHistory = await dbContext.ClassChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageItemDto(m.Role, m.Content))
            .ToListAsync(ct);

        var chatRequest = new ChatRequestDto(
            request.Question.Trim(),
            sortedDocIds,
            recentHistory);

        var ragResponse = await ragClient.ScopedChatAsync(chatRequest, ct);

        var formattedCitations = ragResponse.Citations.Select(c => c with
        {
            ImageUrl = c.PageNumber.HasValue
                ? $"/api/knowledge-documents/{c.DocumentId}/pages/{c.PageNumber.Value}/image"
                : null
        }).ToList();

        var citationsJson = JsonSerializer.Serialize(formattedCitations, JsonOptions);

        if (!string.IsNullOrWhiteSpace(ragResponse.Answer) &&
            !ragResponse.Answer.Contains("chưa tìm thấy thông tin phù hợp", StringComparison.OrdinalIgnoreCase))
        {
            var newCache = new RagAnswerCache
            {
                CacheKey = cacheKey,
                ClassId = classId,
                NormalizedQuestion = normalizedQuestion,
                Answer = ragResponse.Answer,
                CitationsJson = citationsJson,
                HitCount = 1,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };
            dbContext.RagAnswerCaches.Add(newCache);
        }

        var userMsgEntity = new ClassChatMessage
        {
            ConversationId = conversation.Id,
            Role = "user",
            Content = request.Question.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var botMsgEntity = new ClassChatMessage
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = ragResponse.Answer,
            CitationsJson = citationsJson,
            IsFromCache = false,
            CreatedAt = DateTime.UtcNow.AddMilliseconds(50)
        };

        dbContext.ClassChatMessages.AddRange(userMsgEntity, botMsgEntity);
        conversation.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return new ClassChatResultDto(
            conversation.Id,
            request.Question.Trim(),
            ragResponse.Answer,
            formattedCitations,
            false,
            DateTime.UtcNow);
    }

    private static string NormalizeQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return string.Empty;
        var text = question.Trim().ToLowerInvariant();
        text = Regex.Replace(text, @"\s+", " ");
        text = text.TrimEnd('?', '.', '!', ';', ':', ' ');
        return text;
    }

    private static string ComputeSha256(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexStringLower(bytes);
    }

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
