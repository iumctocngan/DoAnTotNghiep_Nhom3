using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Entities;

namespace CmsEdu.Application.Curriculum;

public class CurriculumService(ICurriculumRepository repository, ICurrentUser currentUser)
{
    public async Task<PagedResult<CourseResponse>> GetCoursesAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(page, pageSize);
        var result = await repository.GetCoursesAsync(search?.Trim(), isActive, page, pageSize, cancellationToken);
        return new PagedResult<CourseResponse>(
            result.Items.Select(ToResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalItems);
    }

    public async Task<CourseResponse> GetCourseAsync(int id, CancellationToken cancellationToken = default)
    {
        var course = await repository.GetCourseAsync(id, cancellationToken)
            ?? throw new NotFoundException("Course was not found.");
        return ToResponse(course);
    }

    public async Task<CourseResponse> CreateCourseAsync(
        CourseRequest request,
        CancellationToken cancellationToken = default)
    {
        var data = Validate(request);

        var course = new Course
        {
            Code = data.Code,
            Name = data.Name,
            Description = data.Description,
            IsActive = true
        };

        await repository.SaveCourseAsync(
            course,
            currentUser.UserId,
            "Course.Create",
            $"Created course {course.Code}.",
            cancellationToken);

        return ToResponse(course);
    }

    public async Task<CourseResponse> UpdateCourseAsync(
        int id,
        CourseRequest request,
        CancellationToken cancellationToken = default)
    {
        var data = Validate(request);
        var course = await repository.GetCourseAsync(id, cancellationToken)
            ?? throw new NotFoundException("Course was not found.");

        course.Code = data.Code;
        course.Name = data.Name;
        course.Description = data.Description;

        await repository.SaveCourseAsync(
            course,
            currentUser.UserId,
            "Course.Update",
            $"Updated course {course.Code}.",
            cancellationToken);

        return ToResponse(course);
    }

    public async Task DeactivateCourseAsync(int id, CancellationToken cancellationToken = default)
    {
        var course = await repository.GetCourseAsync(id, cancellationToken)
            ?? throw new NotFoundException("Course was not found.");
        if (!course.IsActive)
            return;

        course.IsActive = false;
        await repository.SaveCourseAsync(
            course,
            currentUser.UserId,
            "Course.Deactivate",
            $"Deactivated course {course.Code}.",
            cancellationToken);
    }

    public async Task<PagedResult<LevelResponse>> GetLevelsAsync(
        int courseId,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateId(courseId, "Course id");
        ValidatePaging(page, pageSize);
        if (await repository.GetCourseAsync(courseId, cancellationToken) is null)
            throw new NotFoundException("Course was not found.");

        var result = await repository.GetLevelsAsync(
            courseId, search?.Trim(), isActive, page, pageSize, cancellationToken);
        return new PagedResult<LevelResponse>(
            result.Items.Select(ToResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalItems);
    }

    public async Task<LevelResponse> GetLevelAsync(int id, CancellationToken cancellationToken = default)
    {
        var level = await repository.GetLevelAsync(id, cancellationToken)
            ?? throw new NotFoundException("Level was not found.");
        return ToResponse(level);
    }

    public async Task<LevelResponse> CreateLevelAsync(
        LevelRequest request,
        CancellationToken cancellationToken = default)
    {
        var data = Validate(request);
        await EnsureActiveCourseAsync(request.CourseId, cancellationToken);

        var level = new Level
        {
            CourseId = request.CourseId,
            Code = data.Code,
            Name = data.Name,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        await repository.SaveLevelAsync(
            level,
            currentUser.UserId,
            "Level.Create",
            $"Created level {level.Code} in course {level.CourseId}.",
            cancellationToken);

        return ToResponse(level);
    }

    public async Task<LevelResponse> UpdateLevelAsync(
        int id,
        LevelRequest request,
        CancellationToken cancellationToken = default)
    {
        var data = Validate(request);
        var level = await repository.GetLevelAsync(id, cancellationToken)
            ?? throw new NotFoundException("Level was not found.");
        if (level.CourseId != request.CourseId)
            throw new ConflictException("Level cannot be moved to another course.");

        await EnsureActiveCourseAsync(request.CourseId, cancellationToken);

        level.Code = data.Code;
        level.Name = data.Name;
        level.SortOrder = request.SortOrder;

        await repository.SaveLevelAsync(
            level,
            currentUser.UserId,
            "Level.Update",
            $"Updated level {level.Code}.",
            cancellationToken);

        return ToResponse(level);
    }

    public async Task DeactivateLevelAsync(int id, CancellationToken cancellationToken = default)
    {
        var level = await repository.GetLevelAsync(id, cancellationToken)
            ?? throw new NotFoundException("Level was not found.");
        if (!level.IsActive)
            return;

        level.IsActive = false;
        await repository.SaveLevelAsync(
            level,
            currentUser.UserId,
            "Level.Deactivate",
            $"Deactivated level {level.Code}.",
            cancellationToken);
    }

    public async Task<PagedResult<LessonResponse>> GetLessonsAsync(
        int levelId,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateId(levelId, "Level id");
        ValidatePaging(page, pageSize);
        if (await repository.GetLevelAsync(levelId, cancellationToken) is null)
            throw new NotFoundException("Level was not found.");

        var result = await repository.GetLessonsAsync(
            levelId, search?.Trim(), isActive, page, pageSize, cancellationToken);
        return new PagedResult<LessonResponse>(
            result.Items.Select(ToResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalItems);
    }

    public async Task<LessonResponse> GetLessonAsync(int id, CancellationToken cancellationToken = default)
    {
        var lesson = await repository.GetLessonAsync(id, cancellationToken)
            ?? throw new NotFoundException("Lesson was not found.");
        return ToResponse(lesson);
    }

    public async Task<LessonResponse> CreateLessonAsync(
        LessonRequest request,
        CancellationToken cancellationToken = default)
    {
        var data = Validate(request);
        await EnsureActiveLevelAsync(request.LevelId, cancellationToken);

        var lesson = new Lesson
        {
            LevelId = request.LevelId,
            Code = data.Code,
            Name = data.Name,
            Objective = data.Objective,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        await repository.SaveLessonAsync(
            lesson,
            currentUser.UserId,
            "Lesson.Create",
            $"Created lesson {lesson.Code} in level {lesson.LevelId}.",
            cancellationToken);

        return ToResponse(lesson);
    }

    public async Task<LessonResponse> UpdateLessonAsync(
        int id,
        LessonRequest request,
        CancellationToken cancellationToken = default)
    {
        var data = Validate(request);
        var lesson = await repository.GetLessonAsync(id, cancellationToken)
            ?? throw new NotFoundException("Lesson was not found.");
        if (lesson.LevelId != request.LevelId)
            throw new ConflictException("Lesson cannot be moved to another level.");

        await EnsureActiveLevelAsync(request.LevelId, cancellationToken);

        lesson.Code = data.Code;
        lesson.Name = data.Name;
        lesson.Objective = data.Objective;
        lesson.SortOrder = request.SortOrder;

        await repository.SaveLessonAsync(
            lesson,
            currentUser.UserId,
            "Lesson.Update",
            $"Updated lesson {lesson.Code}.",
            cancellationToken);

        return ToResponse(lesson);
    }

    public async Task DeactivateLessonAsync(int id, CancellationToken cancellationToken = default)
    {
        var lesson = await repository.GetLessonAsync(id, cancellationToken)
            ?? throw new NotFoundException("Lesson was not found.");
        if (!lesson.IsActive)
            return;

        lesson.IsActive = false;
        await repository.SaveLessonAsync(
            lesson,
            currentUser.UserId,
            "Lesson.Deactivate",
            $"Deactivated lesson {lesson.Code}.",
            cancellationToken);
    }

    private static (string Code, string Name, string? Description) Validate(CourseRequest request)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        var name = request.Name?.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
            throw new ValidationException("Course code is required and must not exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new ValidationException("Course name is required and must not exceed 100 characters.");
        if (description?.Length > 500)
            throw new ValidationException("Course description must not exceed 500 characters.");
        return (code, name, description);
    }

    private static (string Code, string Name) Validate(LevelRequest request)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        var name = request.Name?.Trim();

        ValidateId(request.CourseId, "Course id");
        if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
            throw new ValidationException("Level code is required and must not exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new ValidationException("Level name is required and must not exceed 100 characters.");
        if (request.SortOrder < 1)
            throw new ValidationException("Level sort order must be at least 1.");

        return (code, name);
    }

    private static (string Code, string Name, string? Objective) Validate(LessonRequest request)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        var name = request.Name?.Trim();
        var objective = string.IsNullOrWhiteSpace(request.Objective) ? null : request.Objective.Trim();

        ValidateId(request.LevelId, "Level id");
        if (string.IsNullOrWhiteSpace(code) || code.Length > 50)
            throw new ValidationException("Lesson code is required and must not exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new ValidationException("Lesson name is required and must not exceed 100 characters.");
        if (objective?.Length > 500)
            throw new ValidationException("Lesson objective must not exceed 500 characters.");
        if (request.SortOrder < 1)
            throw new ValidationException("Lesson sort order must be at least 1.");

        return (code, name, objective);
    }

    private async Task EnsureActiveCourseAsync(int courseId, CancellationToken cancellationToken)
    {
        var course = await repository.GetCourseAsync(courseId, cancellationToken)
            ?? throw new NotFoundException("Course was not found.");
        if (!course.IsActive)
            throw new ConflictException("Cannot use an inactive course.");
    }

    private async Task EnsureActiveLevelAsync(int levelId, CancellationToken cancellationToken)
    {
        var level = await repository.GetLevelAsync(levelId, cancellationToken)
            ?? throw new NotFoundException("Level was not found.");
        if (!level.IsActive)
            throw new ConflictException("Cannot use an inactive level.");

        await EnsureActiveCourseAsync(level.CourseId, cancellationToken);
    }

    private static CourseResponse ToResponse(Course course) => new(
        course.Id,
        course.Code,
        course.Name,
        course.Description,
        course.IsActive);

    private static LevelResponse ToResponse(Level level) => new(
        level.Id,
        level.CourseId,
        level.Code,
        level.Name,
        level.SortOrder,
        level.IsActive);

    private static LessonResponse ToResponse(Lesson lesson) => new(
        lesson.Id,
        lesson.LevelId,
        lesson.Code,
        lesson.Name,
        lesson.Objective,
        lesson.SortOrder,
        lesson.IsActive);

    private static void ValidateId(int id, string name)
    {
        if (id < 1)
            throw new ValidationException($"{name} must be at least 1.");
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ValidationException("Page must be at least 1 and pageSize must be between 1 and 100.");
    }
}
