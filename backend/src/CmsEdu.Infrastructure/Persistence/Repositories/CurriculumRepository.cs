using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
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
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Courses.AsNoTracking();
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
            "Course code already exists.", cancellationToken);
    }

    public async Task<PagedResult<Level>> GetLevelsAsync(
        int courseId,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Levels
            .AsNoTracking()
            .Where(level => level.CourseId == courseId);
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

    public Task SaveLevelAsync(
        Level level,
        string? userId,
        string action,
        string description,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(
            level, userId, action, description,
            "Level code or sort order already exists in this course.", cancellationToken);
    }

    public async Task<PagedResult<Lesson>> GetLessonsAsync(
        int levelId,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Lessons
            .AsNoTracking()
            .Where(lesson => lesson.LevelId == levelId);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(lesson => lesson.Code.Contains(search) || lesson.Name.Contains(search));
        if (isActive is not null)
            query = query.Where(lesson => lesson.IsActive == isActive);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(lesson => lesson.SortOrder)
            .ThenBy(lesson => lesson.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Lesson>(items, page, pageSize, totalItems);
    }

    public Task<Lesson?> GetLessonAsync(int id, CancellationToken cancellationToken = default)
    {
        return dbContext.Lessons.SingleOrDefaultAsync(lesson => lesson.Id == id, cancellationToken);
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
            "Lesson code or sort order already exists in this level.", cancellationToken);
    }

    private async Task SaveAsync<TEntity>(
        TEntity entity,
        string? userId,
        string action,
        string description,
        string conflictMessage,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
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
