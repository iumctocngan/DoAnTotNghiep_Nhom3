using CmsEdu.Domain.Common;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Domain.Entities;

public class Session : BaseEntity
{
    public int ClassId { get; set; }
    public Class Class { get; set; } = null!;

    public int? LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    public DateOnly SessionDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;
    public string? Note { get; set; }

    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<StudentRemark> StudentRemarks { get; set; } = new List<StudentRemark>();
}
