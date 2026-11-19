using System.ComponentModel.DataAnnotations;

namespace CmsEdu.Module1;

public record Criterion([Required, MaxLength(200)] string Name,
    decimal Weight, [Required, MaxLength(50)] string Clo,
    [Required, MaxLength(1000)] string Description,
    [MaxLength(5)] string[]? MucDanhGia = null, bool NumericScore = false, bool FixedCriterion = false);
public record CreateRubric(int ClassId, [Required, MaxLength(200)] string Name,
    [Required, MinLength(1), MaxLength(50)] List<Criterion> Criteria);
public record CreateAssessment(int EnrollmentId, int RubricId,
    [Required, MaxLength(200)] string Title, DateOnly Date,
    [Required, MinLength(1), MaxLength(53)] List<decimal> Scores);
public record ApproveFeedback([Required, MaxLength(6000)] string Feedback);
public record GradeRow(int EnrollmentId, [Required, MinLength(1), MaxLength(53)] List<decimal> Scores);
public record SaveClassGrades([Required, MinLength(1)] List<GradeRow> Rows);
public record GenerateLessonClo(Guid DocumentId, int? StartPage, int? EndPage);

public static class AssessmentMath
{
    public static Criterion[] AddFixedCriteria(IReadOnlyList<Criterion> criteria)
    {
        // Allocate exact hundredths while preserving each source criterion's share.
        decimal running = 0;
        var allocated = 0m;
        var lesson = criteria.Select((criterion, index) =>
        {
            running += criterion.Weight;
            var remaining = criteria.Count - index - 1;
            var target = Math.Clamp(Math.Round(running * 0.7m, 2), allocated + 0.01m, 70m - remaining * 0.01m);
            var result = criterion with { Weight = target - allocated, NumericScore = false, FixedCriterion = false };
            allocated = target;
            return result;
        });
        return [..lesson,
            new Criterion("Thái độ học tập", 10, "THAI-DO", "Lắng nghe hướng dẫn và ghi chép nội dung bài học.",
                ["Chưa lắng nghe hoặc chưa ghi chép.", "Lắng nghe và ghi chép khi được nhắc.", "Lắng nghe và ghi được nội dung chính.",
                    "Chủ động lắng nghe, ghi chép đầy đủ.", "Tập trung, ghi chép đầy đủ và có hệ thống."], FixedCriterion: true),
            new Criterion("Kỹ năng học tập", 10, "KY-NANG", "Tham gia làm việc nhóm và thuyết trình kết quả.",
                ["Chưa tham gia nhóm hoặc trình bày.", "Tham gia khi được hỗ trợ, trình bày còn thiếu ý.", "Có đóng góp và trình bày được ý chính.",
                    "Phối hợp tốt và trình bày rõ ràng.", "Chủ động phối hợp, trình bày mạch lạc và giải thích được kết quả."], FixedCriterion: true),
            new Criterion("Điểm sách bài tập", 10, "SACH-BAI-TAP", "Điểm bài làm trong sách bài tập trên thang 10.",
                NumericScore: true, FixedCriterion: true)];
    }

    public static decimal WeightedScore(IReadOnlyList<Criterion> criteria, IReadOnlyList<decimal> scores)
    {
        if (criteria.Count == 0 || criteria.Count != scores.Count ||
            criteria.Any(c => c.Weight <= 0) || criteria.Sum(c => c.Weight) != 100 ||
            scores.Any(s => s < 0 || s > 10))
            throw new ArgumentException("Trọng số phải có tổng 100%; điểm từng tiêu chí từ 0 đến 10.");
        return Math.Round(criteria.Select((c, i) => c.Weight * scores[i]).Sum() / 100, 2);
    }
}
