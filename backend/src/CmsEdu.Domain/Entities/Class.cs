using CmsEdu.Domain.Common;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Domain.Entities;

public class Class : BaseEntity
{
    public string ClassCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int LevelId { get; set; }
    public Level Level { get; set; } = null!;

    public string MainTeacherUserId { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public ClassStatus Status { get; set; } = ClassStatus.Preparing;

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}
