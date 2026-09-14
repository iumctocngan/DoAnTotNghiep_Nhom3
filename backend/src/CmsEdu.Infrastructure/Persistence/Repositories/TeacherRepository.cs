using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Teachers;
using CmsEdu.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Persistence.Repositories;

public class TeacherRepository(AppDbContext dbContext) : ITeacherRepository
{
    public Task<bool> ExistsAsync(string teacherId, CancellationToken cancellationToken = default)
    {
        return (from user in dbContext.Users.AsNoTracking()
                join userRole in dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where user.Id == teacherId && role.Name == UserRole.Teacher
                select user.Id)
            .AnyAsync(cancellationToken);
    }

    public async Task<PagedResult<TeacherClassResponse>> GetClassesAsync(
        string teacherId,
        ClassStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Classes
            .AsNoTracking()
            .Where(item => item.MainTeacherUserId == teacherId);

        if (status is not null)
            query = query.Where(item => item.Status == status);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.ClassCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new TeacherClassResponse(
                item.Id,
                item.ClassCode,
                item.Name,
                item.LevelId,
                item.Level.Name,
                item.Capacity,
                item.StartDate,
                item.EndDate,
                item.DayOfWeek,
                item.StartTime,
                item.EndTime,
                item.Status))
            .ToListAsync(cancellationToken);

        return new PagedResult<TeacherClassResponse>(items, page, pageSize, totalItems);
    }

    public async Task<PagedResult<TeacherScheduleResponse>> GetScheduleAsync(
        string teacherId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Sessions
            .AsNoTracking()
            .Where(item => item.Class.MainTeacherUserId == teacherId);

        if (fromDate is not null)
            query = query.Where(item => item.SessionDate >= fromDate);
        if (toDate is not null)
            query = query.Where(item => item.SessionDate <= toDate);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.SessionDate)
            .ThenBy(item => item.StartTime)
            .ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new TeacherScheduleResponse(
                item.Id,
                item.ClassId,
                item.Class.ClassCode,
                item.Class.Name,
                item.LessonId,
                item.Lesson == null ? null : item.Lesson.Code,
                item.Lesson == null ? null : item.Lesson.Name,
                item.SessionDate,
                item.StartTime,
                item.EndTime,
                item.Status,
                item.Note))
            .ToListAsync(cancellationToken);

        return new PagedResult<TeacherScheduleResponse>(items, page, pageSize, totalItems);
    }
}
