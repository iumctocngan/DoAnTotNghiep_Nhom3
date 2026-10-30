namespace CmsEdu.Domain.Entities;

public class ClassChatConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? ClassId { get; set; }
    public Class? Class { get; set; }
    public string TeacherUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ClassChatMessage> Messages { get; set; } = new List<ClassChatMessage>();
}
