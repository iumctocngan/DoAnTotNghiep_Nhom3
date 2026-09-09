using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Sessions;

public record SessionResponse(
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
