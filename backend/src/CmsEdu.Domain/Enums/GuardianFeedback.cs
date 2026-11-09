namespace CmsEdu.Domain.Enums;

/// <summary>Kết quả phản hồi của phụ huynh sau khi CSKH liên hệ.</summary>
public enum GuardianFeedback
{
    AgreedToRenew      = 1,
    Considering        = 2,
    FinancialDifficulty = 3,
    Dissatisfied       = 4,
    StopLearning       = 5
}
