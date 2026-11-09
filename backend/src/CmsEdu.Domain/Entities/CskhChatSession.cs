namespace CmsEdu.Domain.Entities;

/// <summary>
/// Phiên làm việc của nhân viên CSKH khi tra cứu tình hình một học sinh qua AI Chatbot.
/// SessionId dùng UUID (string) thay vì int để tránh enum hoá và dễ share cross-service.
/// </summary>
public class CskhChatSession
{
    /// <summary>Mã phiên chat (UUID string).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>ID nhân viên CSKH đang dùng chatbot (AspNetUsers.Id).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Học sinh đang được tra cứu.</summary>
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    /// <summary>Tiêu đề phiên, ví dụ: "Tra cứu bé Minh kỳ 2".</summary>
    public string Title { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    public ICollection<CskhChatMessage> Messages { get; set; } = new List<CskhChatMessage>();
}
