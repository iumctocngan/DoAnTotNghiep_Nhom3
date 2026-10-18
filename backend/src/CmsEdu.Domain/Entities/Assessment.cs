namespace CmsEdu.Domain.Entities;

public class AssessmentRubric
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public string Name { get; set; } = "";
    // Immutable definition: preserves the rubric/CLO used by historical grades.
    public string DefinitionJson { get; set; } = "";
}

public class Assessment
{
    public int Id { get; set; }
    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public int RubricId { get; set; }
    public AssessmentRubric Rubric { get; set; } = null!;
    public string Title { get; set; } = "";
    public DateOnly Date { get; set; }
    public decimal Score { get; set; }
    public string ScoresJson { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? Feedback { get; set; }
    public string? FeedbackSource { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
