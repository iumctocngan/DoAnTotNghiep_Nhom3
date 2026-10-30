namespace CmsEdu.Domain.Entities;

public class ClassChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? ClassId { get; set; }
    public Class? Class { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = "user"; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public string? CitationsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
