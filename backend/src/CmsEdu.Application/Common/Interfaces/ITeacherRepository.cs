using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Teachers;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Common.Interfaces;

public interface ITeacherRepository
{
    Task<bool> ExistsAsync(string teacherId, CancellationToken cancellationToken = default);

    Task<PagedResult<TeacherClassResponse>> GetClassesAsync(
        string teacherId,
        ClassStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<TeacherScheduleResponse>> GetScheduleAsync(
        string teacherId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
