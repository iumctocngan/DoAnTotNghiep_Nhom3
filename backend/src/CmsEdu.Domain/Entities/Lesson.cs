using CmsEdu.Domain.Common;

namespace CmsEdu.Domain.Entities;

public class Lesson : BaseEntity
{
    public int LevelId { get; set; }
    public Level Level { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Objective { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}
