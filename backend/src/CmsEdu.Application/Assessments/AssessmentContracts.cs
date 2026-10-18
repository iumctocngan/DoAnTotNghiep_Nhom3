using System.ComponentModel.DataAnnotations;

namespace CmsEdu.Application.Assessments;

public record Criterion([property: Required, MaxLength(200)] string Name,
    decimal Weight, [property: Required, MaxLength(50)] string Clo,
    [property: Required, MaxLength(1000)] string Description);
public record CreateRubric(int ClassId, [property: Required, MaxLength(200)] string Name,
    [property: Required, MinLength(1), MaxLength(50)] List<Criterion> Criteria);
public record CreateAssessment(int EnrollmentId, int RubricId,
    [property: Required, MaxLength(200)] string Title, DateOnly Date,
    [property: Required, MinLength(1), MaxLength(50)] List<decimal> Scores);
public record ApproveFeedback([property: Required, MaxLength(4000)] string Feedback);

public static class AssessmentMath
{
    public static decimal WeightedScore(IReadOnlyList<Criterion> criteria, IReadOnlyList<decimal> scores)
    {
        if (criteria.Count == 0 || criteria.Count != scores.Count ||
            criteria.Any(c => c.Weight <= 0) || criteria.Sum(c => c.Weight) != 100 ||
            scores.Any(s => s < 0 || s > 10))
            throw new ArgumentException("Trọng số phải có tổng 100%; điểm từng tiêu chí từ 0 đến 10.");
        return Math.Round(criteria.Select((c, i) => c.Weight * scores[i]).Sum() / 100, 2);
    }
}
