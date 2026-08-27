using CmsEdu.Domain.Common;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Domain.Entities;

public class Attendance : BaseEntity
{
    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;

    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Note { get; set; }

    public string MarkedBy { get; set; } = string.Empty;
    public DateTime MarkedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
