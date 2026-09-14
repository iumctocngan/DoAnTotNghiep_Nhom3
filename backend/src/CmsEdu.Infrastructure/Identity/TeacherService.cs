using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Teachers;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Identity;

public class TeacherService(AppDbContext dbContext, ICurrentUser currentUser) : ITeacherService
{
    public async Task<PagedResult<TeacherClassResponse>> GetClassesAsync(
        string teacherId,
        ClassStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateAccess(teacherId);
        ValidatePaging(page, pageSize);
        if (status is not null && !Enum.IsDefined(status.Value))
            throw new ValidationException("Class status is invalid.");

        await EnsureTeacherExistsAsync(teacherId, cancellationToken);

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
        ValidateAccess(teacherId);
        ValidatePaging(page, pageSize);
        if (fromDate is not null && toDate is not null && fromDate > toDate)
            throw new ValidationException("From date must not be after to date.");

        await EnsureTeacherExistsAsync(teacherId, cancellationToken);

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

    private void ValidateAccess(string teacherId)
    {
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException();
        if (currentUser.Role == UserRole.Admin)
            return;
        if (currentUser.Role == UserRole.Teacher && currentUser.UserId == teacherId)
            return;

        throw new ForbiddenAccessException("You cannot view another teacher's assignments.");
    }

    private async Task EnsureTeacherExistsAsync(string teacherId, CancellationToken cancellationToken)
    {
        var exists = await (from user in dbContext.Users.AsNoTracking()
                            join userRole in dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                            where user.Id == teacherId && role.Name == UserRole.Teacher
                            select user.Id)
            .AnyAsync(cancellationToken);

        if (!exists)
            throw new NotFoundException("Teacher account was not found.");
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ValidationException("Page must be at least 1 and pageSize must be between 1 and 100.");
    }
}
