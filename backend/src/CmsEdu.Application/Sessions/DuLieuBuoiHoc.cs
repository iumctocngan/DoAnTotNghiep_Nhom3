using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Sessions;

/// <summary>
/// DTO yêu cầu tạo mới buổi học cho một lớp học.
/// </summary>
public sealed record YeuCauTaoBuoiHoc(
    int ClassId,
    int? LessonId,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Note);

/// <summary>
/// DTO yêu cầu cập nhật thông tin buổi học đang ở trạng thái lên lịch.
/// </summary>
public sealed record YeuCauCapNhatBuoiHoc(
    int? LessonId,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Note);

/// <summary>
/// DTO phản hồi thông tin chi tiết của buổi học.
/// </summary>
public sealed record PhanHoiBuoiHoc(
    int Id,
    int ClassId,
    string ClassCode,
    string ClassName,
    int? LessonId,
    string? LessonCode,
    string? LessonName,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    SessionStatus Status,
    string? Note);
