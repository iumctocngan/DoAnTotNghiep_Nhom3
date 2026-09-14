using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Teachers;

public record TeacherScheduleResponse(
    int SessionId,
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
