namespace CmsEdu.Domain.Entities;

public class ClassChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public ClassChatConversation Conversation { get; set; } = null!;
    public string Role { get; set; } = "user"; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public string? CitationsJson { get; set; }
    public bool IsFromCache { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
