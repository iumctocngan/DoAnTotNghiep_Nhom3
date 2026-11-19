using CmsEdu.Application.Curriculum;
using CmsEdu.Domain.Entities;

namespace CmsEdu.UnitTests;

public class LessonOrderingTests
{
    [Fact]
    public void SortsByGroupBeforeWeekAndPreservesUnnumberedAndInactivePositions()
    {
        var lessons = new[]
        {
            MakeLesson(1, "B12 – Tuần 1"),
            MakeLesson(2, "Ôn tập"),
            MakeLesson(3, "B11 Tuần 2"),
            MakeLesson(4, "Tuần 1", active: false),
            MakeLesson(5, "B21 TUẦN 1"),
            MakeLesson(6, "B11 Tuần 1"),
        };

        Assert.Equal(new[] { 6, 2, 3, 4, 1, 5 }, LessonOrdering.ByNumber(lessons).Select(x => x.Id));
    }

    [Fact]
    public void SortsNumberedNamesNumericallyAndIgnoresNumbersInDescriptions()
    {
        var lessons = new[] { MakeLesson(1, "T10"), MakeLesson(2, "Ôn trang 1"), MakeLesson(3, "T2"), MakeLesson(4, "Bài 3") };

        Assert.Equal(new[] { 3, 2, 4, 1 }, LessonOrdering.ByNumber(lessons).Select(x => x.Id));
    }

    [Fact]
    public void SortsUcrGroupsBeforeWeeksAndPlusAfterBaseGroup()
    {
        var lessons = new[]
        {
            MakeLesson(1, "205 Tuần 1"),
            MakeLesson(2, "201 Tuần 4"),
            MakeLesson(3, "202+ Tuần 1"),
            MakeLesson(4, "202 Tuần 2"),
            MakeLesson(5, "201 Tuần 1"),
        };

        Assert.Equal(new[] { 5, 2, 4, 3, 1 }, LessonOrdering.ByNumber(lessons).Select(x => x.Id));
    }

    private static Lesson MakeLesson(int position, string name, bool active = true) =>
        new() { Id = position, SortOrder = position, Name = name, IsActive = active };
}
