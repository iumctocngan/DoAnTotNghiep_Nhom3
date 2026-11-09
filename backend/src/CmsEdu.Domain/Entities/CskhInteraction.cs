using CmsEdu.Domain.Common;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Domain.Entities;

/// <summary>
/// Nhật ký tương tác thực tế sau khi nhân viên CSKH gọi điện / gặp mặt với phụ huynh.
/// Dùng để theo dõi kết quả chiến dịch giữ chân học viên (Retention Outreach).
/// </summary>
public class CskhInteraction : BaseEntity
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int GuardianId { get; set; }
    public Guardian Guardian { get; set; } = null!;

    /// <summary>ID nhân viên thực hiện (AspNetUsers.Id).</summary>
    public string StaffUserId { get; set; } = string.Empty;

    /// <summary>Kênh liên hệ: 1=PhoneCall, 2=Zalo, 3=DirectMeeting.</summary>
    public CskhChannel Channel { get; set; }

    /// <summary>Phản hồi của phụ huynh: 1=AgreedToRenew, 2=Considering, 3=FinancialDifficulty, 4=Dissatisfied, 5=StopLearning.</summary>
    public GuardianFeedback GuardianFeedback { get; set; }

    /// <summary>Ghi chú tóm tắt nội dung trao đổi.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Ngày hẹn liên hệ lại (null nếu không cần theo dõi thêm).</summary>
    public DateOnly? FollowUpDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
