namespace CmsEdu.Domain.Entities;

public class RagAnswerCache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CacheKey { get; set; } = string.Empty;
    public int ClassId { get; set; }
    public string NormalizedQuestion { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? CitationsJson { get; set; }
    public int HitCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
}
