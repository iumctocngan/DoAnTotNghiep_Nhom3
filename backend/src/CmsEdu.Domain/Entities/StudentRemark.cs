using CmsEdu.Domain.Common;

namespace CmsEdu.Domain.Entities;

public class StudentRemark : BaseEntity
{
    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;

    public int? SessionId { get; set; }
    public Session? Session { get; set; }

    public string Content { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
