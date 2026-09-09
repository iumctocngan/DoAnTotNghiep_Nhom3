namespace CmsEdu.Application.Sessions;

public record CreateSessionRequest(
    int ClassId,
    int? LessonId,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Note);
