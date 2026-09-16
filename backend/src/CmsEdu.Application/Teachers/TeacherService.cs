using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Teachers;

public class TeacherService(ITeacherRepository repository, ICurrentUser currentUser)
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
        return await repository.GetClassesAsync(teacherId, status, page, pageSize, cancellationToken);
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
        return await repository.GetScheduleAsync(teacherId, fromDate, toDate, page, pageSize, cancellationToken);
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
        if (!await repository.ExistsAsync(teacherId, cancellationToken))
            throw new NotFoundException("Teacher account was not found.");
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ValidationException("Page must be at least 1 and pageSize must be between 1 and 100.");
    }
}
