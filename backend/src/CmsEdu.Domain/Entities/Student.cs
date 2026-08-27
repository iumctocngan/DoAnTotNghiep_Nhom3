using CmsEdu.Domain.Common;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Domain.Entities;

public class Student : BaseEntity
{
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? LearningNote { get; set; }
    public bool IsArchived { get; set; }

    public ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
