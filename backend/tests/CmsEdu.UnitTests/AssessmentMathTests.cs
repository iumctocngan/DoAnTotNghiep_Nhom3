using CmsEdu.Application.Assessments;

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
}
