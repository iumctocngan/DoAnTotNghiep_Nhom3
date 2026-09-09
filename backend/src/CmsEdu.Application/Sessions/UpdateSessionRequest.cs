namespace CmsEdu.Application.Sessions;

public record UpdateSessionRequest(
    int? LessonId,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Note);
