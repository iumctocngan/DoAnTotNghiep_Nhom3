using CmsEdu.Module1;

namespace CmsEdu.UnitTests;

public class AssessmentMathTests
{
    private static readonly Criterion[] Criteria = [new("Logic", 70, "CLO1", "Tư duy logic"), new("Accuracy", 30, "CLO2", "Tính chính xác")];
    [Fact]
    public void UsesWeightsInsteadOfSimpleAverage() => Assert.Equal(7.4m, AssessmentMath.WeightedScore(Criteria, [8, 6]));
    [Fact]
    public void ZeroIsAValidScore() => Assert.Equal(0, AssessmentMath.WeightedScore(Criteria, [0, 0]));
    [Fact]
    public void RejectsMissingCriteria() => Assert.Throws<ArgumentException>(() => AssessmentMath.WeightedScore(Criteria, [8]));
    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void RejectsOutOfRangeScores(int value) => Assert.Throws<ArgumentException>(() => AssessmentMath.WeightedScore(Criteria, [value, 6]));
    [Fact]
    public void RejectsInvalidWeightTotal() => Assert.Throws<ArgumentException>(() => AssessmentMath.WeightedScore([new("Logic", 90, "CLO1", "Logic")], [8]));

    [Fact]
    public void AddsFixedCriteriaWithoutChangingSourceAndAllowsDecimalWorkbookScore()
    {
        Criterion[] incoming = [new("Logic", 99.99m, "CLO1", "Logic"), new("Accuracy", .01m, "CLO2", "Accuracy")];
        var rubric = AssessmentMath.AddFixedCriteria(incoming);

        Assert.Equal(100m, rubric.Sum(x => x.Weight));
        Assert.All(rubric, x => Assert.True(x.Weight > 0));
        Assert.Equal(70m, rubric.Where(x => !x.FixedCriterion).Sum(x => x.Weight));
        Assert.Equal(3, rubric.Count(x => x.FixedCriterion));
        Assert.Equal("SACH-BAI-TAP", Assert.Single(rubric, x => x.NumericScore).Clo);
        Assert.Equal(7.85m, AssessmentMath.WeightedScore(rubric, [8, 8, 8, 8, 6.5m]));
        Assert.Equal(99.99m, incoming[0].Weight);
    }
}
