using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Curriculum;
using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Common;
using CmsEdu.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Persistence.Repositories;

public class CurriculumRepository(AppDbContext dbContext) : ICurriculumRepository
{
    public async Task<PagedResult<Course>> GetCoursesAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        IEnumerable<int>? allowedIds = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Courses.AsNoTracking();
        if (allowedIds is not null)
        {
            var ids = allowedIds.ToList();
            query = query.Where(course => ids.Contains(course.Id));
        }

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(course => course.Code.Contains(search) || course.Name.Contains(search));
        if (isActive is not null)
            query = query.Where(course => course.IsActive == isActive);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(course => course.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Course>(items, page, pageSize, totalItems);
    }

    public Task<Course?> GetCourseAsync(int id, CancellationToken cancellationToken = default)
    {
        return dbContext.Courses.SingleOrDefaultAsync(course => course.Id == id, cancellationToken);
    }

    public Task SaveCourseAsync(
        Course course,
        string? userId,
        string action,
        string description,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(
            course, userId, action, description,
            "Mã khóa học đã tồn tại.", cancellationToken);
    }

    public async Task<PagedResult<Level>> GetLevelsAsync(
        int courseId,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        IEnumerable<int>? allowedIds = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Levels
            .AsNoTracking()
            .Where(level => level.CourseId == courseId);

        if (allowedIds is not null)
        {
            var ids = allowedIds.ToList();
            query = query.Where(level => ids.Contains(level.Id));
        }

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(level => level.Code.Contains(search) || level.Name.Contains(search));
        if (isActive is not null)
            query = query.Where(level => level.IsActive == isActive);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(level => level.SortOrder)
            .ThenBy(level => level.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Level>(items, page, pageSize, totalItems);
    }

    public Task<Level?> GetLevelAsync(int id, CancellationToken cancellationToken = default)
    {
        return dbContext.Levels.SingleOrDefaultAsync(level => level.Id == id, cancellationToken);
    }

    public async Task<int> GetNextLevelSortOrderAsync(int courseId, CancellationToken cancellationToken = default)
    {
        var max = await dbContext.Levels
            .Where(l => l.CourseId == courseId)
            .MaxAsync(l => (int?)l.SortOrder, cancellationToken);
        return (max ?? 0) + 1;
    }

    public Task SaveLevelAsync(
        Level level,
        string? userId,
        string action,
        string description,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(
            level, userId, action, description,
            "Mã cấp độ đã tồn tại trong khóa học này.", cancellationToken);
    }

    public async Task<List<int>> GetTeacherCourseIdsAsync(string teacherUserId, CancellationToken ct = default)
    {
        return await dbContext.Classes
            .AsNoTracking()
            .Where(c => c.MainTeacherUserId == teacherUserId)
            .Select(c => c.Level.CourseId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<List<int>> GetTeacherLevelIdsAsync(string teacherUserId, CancellationToken ct = default)
    {
        return await dbContext.Classes
            .AsNoTracking()
            .Where(c => c.MainTeacherUserId == teacherUserId)
            .Select(c => c.LevelId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<PagedResult<Lesson>> GetLessonsAsync(
        int levelId,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Lessons.AsNoTracking().Where(lesson => lesson.LevelId == levelId);
        var ordered = LessonOrdering.ByNumber(await query.ToListAsync(cancellationToken));
        AssignLessonOrder(ordered);
        IEnumerable<Lesson> filtered = ordered;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var matchingIds = (await query.Where(lesson => lesson.Code.Contains(search) || lesson.Name.Contains(search))
                .Select(lesson => lesson.Id).ToListAsync(cancellationToken)).ToHashSet();
            filtered = filtered.Where(lesson => matchingIds.Contains(lesson.Id));
        }
        if (isActive is not null)
            filtered = filtered.Where(lesson => lesson.IsActive == isActive);

        var results = filtered.ToList();
        return new PagedResult<Lesson>(results.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            page, pageSize, results.Count);
    }

    public Task<Lesson?> GetLessonAsync(int id, CancellationToken cancellationToken = default)
    {
        return dbContext.Lessons.SingleOrDefaultAsync(lesson => lesson.Id == id, cancellationToken);
    }

    public async Task<int> ReorderNumberedLessonsAsync(int levelId, string? userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var lessons = await dbContext.Lessons.Where(x => x.LevelId == levelId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var ordered = LessonOrdering.ByNumber(lessons);
        var updated = AssignLessonOrder(ordered);
        if (updated == 0) return 0;
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "Lesson.ReorderByNumber",
            EntityType = nameof(Level),
            EntityId = levelId.ToString(),
            Description = $"Reordered {updated} lessons in level {levelId}.",
            OccurredAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    private static int AssignLessonOrder(IReadOnlyList<Lesson> ordered)
    {
        var updated = 0;
        for (var index = 0; index < ordered.Count; index++)
        {
            var lesson = ordered[index];
            if (lesson.SortOrder == index + 1) continue;
            lesson.SortOrder = index + 1;
            updated++;
        }
        return updated;
    }

    public Task SaveLessonAsync(
        Lesson lesson,
        string? userId,
        string action,
        string description,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(
            lesson, userId, action, description,
            "Mã bài học đã tồn tại trong cấp độ này.", cancellationToken, async () =>
            {
                var levelLessons = await dbContext.Lessons.Where(x => x.LevelId == lesson.LevelId).ToListAsync(cancellationToken);
                AssignLessonOrder(LessonOrdering.ByNumber(levelLessons, lesson.Id == 0 ? lesson : null));
            });
    }

    private async Task SaveAsync<TEntity>(
        TEntity entity,
        string? userId,
        string action,
        string description,
        string conflictMessage,
        CancellationToken cancellationToken,
        Func<Task>? prepare = null)
        where TEntity : BaseEntity
    {
        await using var transaction = prepare == null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        if (prepare != null)
            await prepare();
        if (entity.Id == 0)
            dbContext.Set<TEntity>().Add(entity);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityType = typeof(TEntity).Name,
                EntityId = entity.Id.ToString(),
                Description = description,
                OccurredAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when
            (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException(conflictMessage);
        }
    }
}
