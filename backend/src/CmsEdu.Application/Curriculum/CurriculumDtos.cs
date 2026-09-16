namespace CmsEdu.Application.Curriculum;

public record CourseRequest(string Code, string Name, string? Description);

public record CourseResponse(int Id, string Code, string Name, string? Description, bool IsActive);

public record LevelRequest(int CourseId, string Code, string Name, int SortOrder);

public record LevelResponse(int Id, int CourseId, string Code, string Name, int SortOrder, bool IsActive);

public record LessonRequest(int LevelId, string Code, string Name, string? Objective, int SortOrder);

public record LessonResponse(int Id, int LevelId, string Code, string Name, string? Objective, int SortOrder, bool IsActive);
