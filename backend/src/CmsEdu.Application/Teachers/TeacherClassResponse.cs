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
    ClassStatus Status);
