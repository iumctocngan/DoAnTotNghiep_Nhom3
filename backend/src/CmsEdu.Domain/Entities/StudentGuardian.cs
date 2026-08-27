namespace CmsEdu.Domain.Entities;

public class StudentGuardian
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int GuardianId { get; set; }
    public Guardian Guardian { get; set; } = null!;

    public string Relationship { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
