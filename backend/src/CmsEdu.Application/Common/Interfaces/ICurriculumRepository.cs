using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Entities;

namespace CmsEdu.Application.Common.Interfaces;

public interface ICurriculumRepository
{
    Task<PagedResult<Course>> GetCoursesAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Course?> GetCourseAsync(int id, CancellationToken cancellationToken = default);

    Task SaveCourseAsync(
        Course course,
        string? userId,
        string action,
        string description,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Level>> GetLevelsAsync(
        int courseId,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Level?> GetLevelAsync(int id, CancellationToken cancellationToken = default);

    Task SaveLevelAsync(
        Level level,
        string? userId,
        string action,
        string description,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Lesson>> GetLessonsAsync(
        int levelId,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Lesson?> GetLessonAsync(int id, CancellationToken cancellationToken = default);

    Task SaveLessonAsync(
        Lesson lesson,
        string? userId,
        string action,
        string description,
        CancellationToken cancellationToken = default);
}
