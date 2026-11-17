using CmsEdu.Application.AttendanceReports;
using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ValidationException = CmsEdu.Application.Common.Exceptions.ValidationException;

namespace CmsEdu.Infrastructure.Services;

public sealed class AttendanceAbsenceReportService(
    AppDbContext dbContext,
    ICurrentUser currentUser) : IAttendanceAbsenceReportService
{
    private const int MaximumPageSize = 100;

    public async Task<PagedResult<AttendanceAbsenceReportResponse>> GetAbsenceReportAsync(
        int? courseId,
        int? classId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(courseId, classId, fromDate, toDate, page, pageSize);
        EnsureCanViewReport();

        var query = dbContext.Attendances
            .AsNoTracking()
            .Where(item =>
                item.Status == AttendanceStatus.Absent &&
                item.Session.Status == SessionStatus.Completed);

        if (courseId.HasValue)
        {
            query = query.Where(item => item.Session.Class.Level.CourseId == courseId.Value);
        }

        if (classId.HasValue)
        {
            query = query.Where(item => item.Session.ClassId == classId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(item => item.Session.SessionDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(item => item.Session.SessionDate <= toDate.Value);
        }

        if (currentUser.Role == UserRole.Teacher)
        {
            var teacherId = currentUser.UserId!;
            query = query.Where(item => item.Session.Class.MainTeacherUserId == teacherId);
        }

        var groupedQuery = query
            .GroupBy(item => new
            {
                item.Enrollment.StudentId,
                item.Enrollment.Student.StudentCode,
                StudentFullName = item.Enrollment.Student.FullName,
                CourseId = item.Session.Class.Level.CourseId,
                CourseCode = item.Session.Class.Level.Course.Code,
                CourseName = item.Session.Class.Level.Course.Name
            });

        var totalItems = await groupedQuery.CountAsync(cancellationToken);
        var items = await groupedQuery
            .OrderBy(item => item.Key.CourseName)
            .ThenBy(item => item.Key.StudentFullName)
            .ThenBy(item => item.Key.StudentId)
            .ThenBy(item => item.Key.CourseId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AttendanceAbsenceReportResponse(
                item.Key.StudentId,
                item.Key.StudentCode,
                item.Key.StudentFullName,
                item.Key.CourseId,
                item.Key.CourseCode,
                item.Key.CourseName,
                item.Count()))
            .ToListAsync(cancellationToken);

        return new PagedResult<AttendanceAbsenceReportResponse>(
            items, page, pageSize, totalItems);
    }

    private void EnsureCanViewReport()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Chưa xác thực người dùng.");
        }

        if (currentUser.Role == UserRole.Admin)
        {
            return;
        }

        if (currentUser.Role != UserRole.Teacher ||
            string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem báo cáo chuyên cần.");
        }
    }

    private static void ValidateRequest(
        int? courseId,
        int? classId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int pageSize)
    {
        if (courseId.HasValue && courseId.Value <= 0)
        {
            throw new ValidationException("courseId phải lớn hơn 0.");
        }

        if (classId.HasValue && classId.Value <= 0)
        {
            throw new ValidationException("classId phải lớn hơn 0.");
        }

        if (page < 1 ||
            pageSize < 1 ||
            pageSize > MaximumPageSize ||
            (long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ValidationException("Trang phải từ 1 trở lên và kích thước trang phải từ 1 đến 100.");
        }

        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            throw new ValidationException("fromDate không được sau toDate.");
        }
    }
}
