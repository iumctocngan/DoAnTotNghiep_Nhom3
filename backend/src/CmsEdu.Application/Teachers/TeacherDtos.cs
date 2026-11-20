using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Teachers;

public record TeacherClassResponse(
    int Id,
    string ClassCode,
    string Name,
    int LevelId,
    string LevelName,
    int Capacity,
    DateOnly StartDate,
    DateOnly? EndDate,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    ClassStatus Status,
    int CurrentEnrollmentCount = 0);

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
    string? Note,
    string? TeacherName = null);
