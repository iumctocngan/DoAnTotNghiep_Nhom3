using CmsEdu.Domain.Common;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Domain.Entities;

public class Enrollment : BaseEntity
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int ClassId { get; set; }
    public Class Class { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

    public string? EndReason { get; set; }
    public string? PauseReason { get; set; }
    public DateOnly? ExpectedReturnDate { get; set; }

    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<StudentRemark> StudentRemarks { get; set; } = new List<StudentRemark>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
