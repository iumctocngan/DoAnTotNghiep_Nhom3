using System.Text.RegularExpressions;
using CmsEdu.Domain.Entities;

namespace CmsEdu.Application.Curriculum;

public static partial class LessonOrdering
{
    [GeneratedRegex(@"\btuần\s*[:.-]?\s*(?<number>[0-9]+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WeekNumber();

    [GeneratedRegex(@"^\s*(?:bài\s*[:.-]?\s*|(?<prefix>[a-z]{1,3})\s*)(?<number>[0-9]+)(?=\s|[-–:.)]|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LessonNumber();

    [GeneratedRegex(@"^\s*(?<number>[0-9]+)(?<plus>\+)?(?=\s|[-–:.)]|$)")]
    private static partial Regex NumericGroup();

    public static IReadOnlyList<Lesson> ByNumber(IEnumerable<Lesson> lessons, Lesson? appended = null)
    {
        var existing = lessons.OrderBy(x => x.SortOrder).ThenBy(x => x.Id);
        var current = (appended == null ? existing : existing.Append(appended))
            .Select(x => (Lesson: x, Order: x.IsActive ? ReadOrder(x.Name) : default)).ToArray();
        var grouped = new Queue<Lesson>(current.Where(x => x.Order.Prefix != null)
            .OrderBy(x => x.Order.Prefix, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Order.GroupNumber).ThenBy(x => x.Order.Plus).ThenBy(x => x.Order.Number).Select(x => x.Lesson));
        var numbered = new Queue<Lesson>(current.Where(x => x.Order.Prefix == null && x.Order.Number.HasValue)
            .OrderBy(x => x.Order.Number).Select(x => x.Lesson));
        return current.Select(x => x.Order.Prefix != null ? grouped.Dequeue() :
            x.Order.Number.HasValue ? numbered.Dequeue() : x.Lesson).ToArray();
    }

    private static (string? Prefix, int? GroupNumber, bool Plus, int? Number) ReadOrder(string name)
    {
        var lesson = LessonNumber().Match(name);
        var week = ReadNumber(WeekNumber().Match(name));
        var lessonNumber = ReadNumber(lesson);
        var prefix = lesson.Groups["prefix"].Value;
        if (week.HasValue && lessonNumber.HasValue && prefix.Length > 0)
            return (prefix, lessonNumber, false, week);
        var numericGroup = NumericGroup().Match(name);
        var groupNumber = ReadNumber(numericGroup);
        if (week.HasValue && groupNumber.HasValue)
            return ("", groupNumber, numericGroup.Groups["plus"].Success, week);
        return (null, null, false, week ?? lessonNumber);
    }

    private static int? ReadNumber(Match match) =>
        match.Success && int.TryParse(match.Groups["number"].Value, out var number) && number > 0 ? number : null;
}
