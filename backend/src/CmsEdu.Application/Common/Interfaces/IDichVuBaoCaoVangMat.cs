using CmsEdu.Application.AttendanceReports;
using CmsEdu.Application.Common.Models;

namespace CmsEdu.Application.Common.Interfaces;

public interface IAttendanceAbsenceReportService
{
    Task<PagedResult<AttendanceAbsenceReportResponse>> GetAbsenceReportAsync(
        int? courseId,
        int? classId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
