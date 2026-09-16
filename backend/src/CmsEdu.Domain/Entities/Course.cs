using CmsEdu.Domain.Common;

namespace CmsEdu.Domain.Entities;

public class Course : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Level> Levels { get; set; } = new List<Level>();
}
