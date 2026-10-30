using CmsEdu.Domain.Enums;

namespace CmsEdu.Domain.Entities;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Status { get; set; } = "processing";
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public KnowledgeScope Scope { get; set; } = KnowledgeScope.Global;
    public int? CourseId { get; set; }
    public Course? Course { get; set; }
    public int? LevelId { get; set; }
    public Level? Level { get; set; }
    public int? ClassId { get; set; }
    public Class? Class { get; set; }
    public string? UploadedByUserId { get; set; }
}
