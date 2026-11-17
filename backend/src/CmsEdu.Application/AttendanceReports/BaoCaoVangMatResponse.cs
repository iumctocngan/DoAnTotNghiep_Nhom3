namespace CmsEdu.Application.AttendanceReports;

/// <summary>Thông tin tổng hợp số buổi vắng của một học viên trong một khóa học.</summary>
public sealed record AttendanceAbsenceReportResponse(
    int StudentId,
    string StudentCode,
    string StudentFullName,
    int CourseId,
    string CourseCode,
    string CourseName,
    int AbsenceCount);
