using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Curriculum;
using CmsEdu.Application.Knowledge;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Infrastructure.Persistence.Repositories;
using CmsEdu.Infrastructure.Services.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.UnitTests;

public class ClassChatAndKnowledgeTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private sealed class TestCurrentUser(string? userId, string? role) : ICurrentUser
    {
        public string? UserId => userId;
        public string? Role => role;
        public bool IsAuthenticated => userId != null;
    }

    private sealed class TestRagModuleClient : IRagModuleClient
    {
        public int ScopedChatCallCount { get; private set; }
        public string ResponseAnswer { get; set; } = "Mục tiêu bài 1 là rèn luyện tư duy số học.";

        public Task<ChatResponseDto> ScopedChatAsync(ChatRequestDto request, CancellationToken ct)
        {
            ScopedChatCallCount++;
            return Task.FromResult(new ChatResponseDto(ResponseAnswer, []));
        }

        public Task<bool> IngestKnowledgeDocumentAsync(Guid documentId, string fileName, Stream fileStream, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> DeleteKnowledgeDocumentAsync(Guid documentId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<Stream> GetKnowledgeDocumentPageImageStreamAsync(Guid documentId, int page, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream());
    }

    [Fact]
    public async Task GetClassKnowledgeInfoAsync_InheritsDocumentsAcrossGlobalCourseLevelClass()
    {
        var db = CreateInMemoryDbContext();
        var teacherId = "teacher-123";

        var course = new Course { Id = 1, Name = "Toán Tư Duy Ucrea", Description = "Khóa 1" };
        var level = new Level { Id = 10, CourseId = 1, Course = course, Code = "L1", Name = "Level 1" };
        var otherLevel = new Level { Id = 20, CourseId = 1, Course = course, Code = "L2", Name = "Level 2" };
        var cls = new Class
        {
            Id = 100,
            Name = "Lớp Mầm Chồi 01",
            ClassCode = "MC01",
            LevelId = 10,
            Level = level,
            MainTeacherUserId = teacherId
        };

        db.Courses.Add(course);
        db.Levels.AddRange(level, otherLevel);
        db.Classes.Add(cls);

        var docGlobal = new Document { Id = Guid.NewGuid(), Title = "Quy định chung", FileName = "qd.pdf", DocumentType = "pdf", Scope = KnowledgeScope.Global, Status = "ready" };
        var docCourse = new Document { Id = Guid.NewGuid(), Title = "Giáo trình Ucrea", FileName = "ucrea.pdf", DocumentType = "pdf", Scope = KnowledgeScope.Course, CourseId = 1, Status = "ready" };
        var docLevel = new Document { Id = Guid.NewGuid(), Title = "Kế hoạch bài dạy Level 1", FileName = "l1.docx", DocumentType = "docx", Scope = KnowledgeScope.Level, LevelId = 10, Status = "ready" };
        var docClass = new Document { Id = Guid.NewGuid(), Title = "Lưu ý riêng MC01", FileName = "mc01.pdf", DocumentType = "pdf", Scope = KnowledgeScope.Class, ClassId = 100, Status = "ready" };
        var docOtherLevel = new Document { Id = Guid.NewGuid(), Title = "Kế hoạch Level 2", FileName = "l2.docx", DocumentType = "docx", Scope = KnowledgeScope.Level, LevelId = 20, Status = "ready" };

        db.Documents.AddRange(docGlobal, docCourse, docLevel, docClass, docOtherLevel);
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(teacherId, UserRole.Teacher);
        var rag = new TestRagModuleClient();
        var service = new ClassChatService(db, user, rag);

        var info = await service.GetClassKnowledgeInfoAsync(100, CancellationToken.None);

        Assert.Equal(100, info.ClassId);
        Assert.Equal("Lớp Mầm Chồi 01", info.ClassName);
        Assert.Equal(4, info.Documents.Count);
        Assert.Contains(info.Documents, d => d.Title == "Quy định chung");
        Assert.Contains(info.Documents, d => d.Title == "Giáo trình Ucrea");
        Assert.Contains(info.Documents, d => d.Title == "Kế hoạch bài dạy Level 1");
        Assert.Contains(info.Documents, d => d.Title == "Lưu ý riêng MC01");
        Assert.DoesNotContain(info.Documents, d => d.Title == "Kế hoạch Level 2");
    }

    [Fact]
    public async Task ChatAsync_CachesExactAnswerAndIncrementsHitCount()
    {
        var db = CreateInMemoryDbContext();
        var teacherId = "teacher-123";

        var course = new Course { Id = 1, Name = "Toán Tư Duy", Description = "Khóa 1" };
        var level = new Level { Id = 10, CourseId = 1, Course = course, Code = "L1", Name = "Level 1" };
        var cls = new Class
        {
            Id = 100,
            Name = "Lớp Toán MC01",
            ClassCode = "MC01",
            LevelId = 10,
            Level = level,
            MainTeacherUserId = teacherId
        };
        db.Courses.Add(course);
        db.Levels.Add(level);
        db.Classes.Add(cls);

        var doc = new Document { Id = Guid.NewGuid(), Title = "Tài liệu 1", FileName = "t1.docx", DocumentType = "docx", Scope = KnowledgeScope.Class, ClassId = 100, Status = "ready" };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(teacherId, UserRole.Teacher);
        var rag = new TestRagModuleClient();
        var service = new ClassChatService(db, user, rag);

        // 1st request -> Cache miss
        var req = new ClassChatRequestDto("Mục tiêu của bài học số 1 là gì?");
        var result1 = await service.ChatAsync(100, req, CancellationToken.None);

        Assert.False(result1.IsFromCache);
        Assert.Equal("Mục tiêu bài 1 là rèn luyện tư duy số học.", result1.Answer);
        Assert.Equal(1, rag.ScopedChatCallCount);

        // Verify cache entry exists in DB
        var cacheEntry = await db.RagAnswerCaches.FirstOrDefaultAsync(c => c.ClassId == 100);
        Assert.NotNull(cacheEntry);
        Assert.Equal(1, cacheEntry.HitCount);

        // 2nd request with minor punctuation / casing difference -> Should be a Cache Hit!
        var req2 = new ClassChatRequestDto("Mục tiêu của bài học số 1 là gì", result1.ConversationId);
        var result2 = await service.ChatAsync(100, req2, CancellationToken.None);

        Assert.True(result2.IsFromCache);
        Assert.Equal("Mục tiêu bài 1 là rèn luyện tư duy số học.", result2.Answer);
        Assert.Equal(1, rag.ScopedChatCallCount);

        // Cache HitCount should now be 2
        var updatedCache = await db.RagAnswerCaches.FirstAsync(c => c.Id == cacheEntry.Id);
        Assert.Equal(2, updatedCache.HitCount);
    }

    [Fact]
    public async Task ChatAsync_UnauthorizedTeacher_ThrowsForbiddenException()
    {
        var db = CreateInMemoryDbContext();
        var teacherId = "teacher-123";
        var otherTeacherId = "teacher-other";

        var course = new Course { Id = 1, Name = "Toán Tư Duy", Description = "Khóa 1" };
        var level = new Level { Id = 10, CourseId = 1, Course = course, Code = "L1", Name = "Level 1" };
        var cls = new Class
        {
            Id = 100,
            Name = "Lớp Toán MC01",
            ClassCode = "MC01",
            LevelId = 10,
            Level = level,
            MainTeacherUserId = teacherId
        };
        db.Courses.Add(course);
        db.Levels.Add(level);
        db.Classes.Add(cls);
        await db.SaveChangesAsync();

        var user = new TestCurrentUser(otherTeacherId, UserRole.Teacher);
        var rag = new TestRagModuleClient();
        var service = new ClassChatService(db, user, rag);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            service.ChatAsync(100, new ClassChatRequestDto("Xin chào?"), CancellationToken.None));
    }

    [Fact]
    public async Task GetCoursesAsync_Teacher_OnlyReturnsAssignedCourses()
    {
        var db = CreateInMemoryDbContext();
        var teacherId = "teacher-123";

        var course1 = new Course { Id = 1, Code = "KH01", Name = "Toán Tư Duy", IsActive = true };
        var course2 = new Course { Id = 2, Code = "KH02", Name = "Tiếng Anh Giao Tiếp", IsActive = true };
        var level1 = new Level { Id = 10, CourseId = 1, Course = course1, Code = "L1", Name = "Level 1", IsActive = true, SortOrder = 1 };
        var level2 = new Level { Id = 20, CourseId = 2, Course = course2, Code = "L2", Name = "Level 2", IsActive = true, SortOrder = 1 };
        var cls = new Class { Id = 100, Name = "Lớp 1", ClassCode = "C1", LevelId = 10, Level = level1, MainTeacherUserId = teacherId };

        db.Courses.AddRange(course1, course2);
        db.Levels.AddRange(level1, level2);
        db.Classes.Add(cls);
        await db.SaveChangesAsync();

        var repo = new CurriculumRepository(db);
        var teacherUser = new TestCurrentUser(teacherId, UserRole.Teacher);
        var service = new CurriculumService(repo, teacherUser);

        var courses = await service.GetCoursesAsync(null, null, 1, 10, CancellationToken.None);
        Assert.Single(courses.Items);
        Assert.Equal("KH01", courses.Items[0].Code);

        // Accessing Course 2 directly should throw ForbiddenAccessException
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            service.GetCourseAsync(2, CancellationToken.None));
    }

    [Fact]
    public async Task GetDocumentsAsync_Teacher_OnlyReturnsScopedKnowledgeDocuments()
    {
        var db = CreateInMemoryDbContext();
        var teacherId = "teacher-123";

        var course1 = new Course { Id = 1, Name = "Toán Tư Duy" };
        var course2 = new Course { Id = 2, Name = "Tiếng Anh" };
        var level1 = new Level { Id = 10, CourseId = 1, Course = course1, Code = "L1", Name = "Level 1" };
        var level2 = new Level { Id = 20, CourseId = 2, Course = course2, Code = "L2", Name = "Level 2" };
        var cls = new Class { Id = 100, Name = "Lớp 1", ClassCode = "C1", LevelId = 10, Level = level1, MainTeacherUserId = teacherId };

        db.Courses.AddRange(course1, course2);
        db.Levels.AddRange(level1, level2);
        db.Classes.Add(cls);

        var docGlobal = new Document { Id = Guid.NewGuid(), Title = "Quy định", FileName = "qd.pdf", DocumentType = "pdf", Scope = KnowledgeScope.Global, Status = "ready" };
        var docCourse1 = new Document { Id = Guid.NewGuid(), Title = "Giáo trình Toán", FileName = "toan.pdf", DocumentType = "pdf", Scope = KnowledgeScope.Course, CourseId = 1, Status = "ready" };
        var docCourse2 = new Document { Id = Guid.NewGuid(), Title = "Giáo trình Anh", FileName = "anh.pdf", DocumentType = "pdf", Scope = KnowledgeScope.Course, CourseId = 2, Status = "ready" };

        db.Documents.AddRange(docGlobal, docCourse1, docCourse2);
        await db.SaveChangesAsync();

        var teacherUser = new TestCurrentUser(teacherId, UserRole.Teacher);
        var rag = new TestRagModuleClient();
        var docService = new KnowledgeDocumentService(db, teacherUser, rag);

        var docs = await docService.GetDocumentsAsync(null, null, null, null, null, CancellationToken.None);
        Assert.Equal(2, docs.Count);
        Assert.Contains(docs, d => d.Title == "Quy định");
        Assert.Contains(docs, d => d.Title == "Giáo trình Toán");
        Assert.DoesNotContain(docs, d => d.Title == "Giáo trình Anh");
    }

    [Fact]
    public async Task ChatAsync_Admin_CenterWideChat_ReturnsAllCenterKnowledgeDocs()
    {
        var db = CreateInMemoryDbContext();
        var adminId = "admin-123";

        var course1 = new Course { Id = 1, Name = "Toán Tư Duy" };
        var course2 = new Course { Id = 2, Name = "Tiếng Anh" };
        var level1 = new Level { Id = 10, CourseId = 1, Course = course1, Code = "L1", Name = "Level 1" };
        var level2 = new Level { Id = 20, CourseId = 2, Course = course2, Code = "L2", Name = "Level 2" };
        var cls = new Class { Id = 100, Name = "Lớp 1", ClassCode = "C1", LevelId = 10, Level = level1, MainTeacherUserId = "teacher-1" };

        db.Courses.AddRange(course1, course2);
        db.Levels.AddRange(level1, level2);
        db.Classes.Add(cls);

        var docGlobal = new Document { Id = Guid.NewGuid(), Title = "Quy định", FileName = "qd.pdf", DocumentType = "pdf", Scope = KnowledgeScope.Global, Status = "ready" };
        var docCourse1 = new Document { Id = Guid.NewGuid(), Title = "Giáo trình Toán", FileName = "toan.pdf", DocumentType = "pdf", Scope = KnowledgeScope.Course, CourseId = 1, Status = "ready" };
        var docCourse2 = new Document { Id = Guid.NewGuid(), Title = "Giáo trình Anh", FileName = "anh.pdf", DocumentType = "pdf", Scope = KnowledgeScope.Course, CourseId = 2, Status = "ready" };

        db.Documents.AddRange(docGlobal, docCourse1, docCourse2);
        await db.SaveChangesAsync();

        var adminUser = new TestCurrentUser(adminId, UserRole.Admin);
        var rag = new TestRagModuleClient();
        var service = new ClassChatService(db, adminUser, rag);

        var info = await service.GetClassKnowledgeInfoAsync(0, CancellationToken.None);
        Assert.Equal(3, info.Documents.Count);
        Assert.Equal("Toàn bộ Trung tâm", info.ClassName);

        var chatRes = await service.ChatAsync(0, new ClassChatRequestDto("Hỏi đáp toàn trung tâm"), CancellationToken.None);
        Assert.NotNull(chatRes);
        Assert.Equal("Mục tiêu bài 1 là rèn luyện tư duy số học.", chatRes.Answer);

        // Teacher should NOT be able to access center-wide chat (classId = 0)
        var teacherUser = new TestCurrentUser("teacher-1", UserRole.Teacher);
        var teacherService = new ClassChatService(db, teacherUser, rag);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            teacherService.GetClassKnowledgeInfoAsync(0, CancellationToken.None));
    }
}
