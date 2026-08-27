using CmsEdu.Domain.Common;

namespace CmsEdu.Domain.Entities;

public class Level : BaseEntity
{
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
    public ICollection<Class> Classes { get; set; } = new List<Class>();
}
