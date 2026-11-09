using CmsEdu.Domain.Common;

namespace CmsEdu.Domain.Entities;

/// <summary>
/// Chi tiết từng tin nhắn trong phiên AI Chatbot CSKH.
/// Lưu cả snapshot dữ liệu đã inject vào prompt và kịch bản tư vấn do LLM sinh ra.
/// </summary>
public class CskhChatMessage : BaseEntity
{
    public string SessionId { get; set; } = string.Empty;
    public CskhChatSession Session { get; set; } = null!;

    /// <summary>"User" (nhân viên CSKH) hoặc "Assistant" (AI Bot).</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Nội dung văn bản câu hỏi hoặc câu trả lời.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Snapshot JSON dữ liệu CSDL được tiêm vào prompt (chỉ set cho tin nhắn "Assistant").</summary>
    public string? ContextDataJson { get; set; }

    /// <summary>JSON cấu trúc kịch bản tư vấn gọi điện cho phụ huynh (chỉ set cho "Assistant").</summary>
    public string? SuggestedScriptJson { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
