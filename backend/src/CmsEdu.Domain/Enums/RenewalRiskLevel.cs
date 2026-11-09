namespace CmsEdu.Domain.Enums;

/// <summary>
/// Mức độ rủi ro không tái phí của học sinh (dùng trong Module 3 AI Prediction).
/// </summary>
public enum RenewalRiskLevel
{
    /// <summary>Nguy cơ cao — xác suất tái phí dưới 50%. Cần CSKH can thiệp ngay.</summary>
    HighRisk = 1,

    /// <summary>Nguy cơ trung bình — xác suất tái phí 50–74%. Theo dõi và chăm sóc định kỳ.</summary>
    MediumRisk = 2,

    /// <summary>Nguy cơ thấp — xác suất tái phí từ 75% trở lên. Học viên tiềm năng cao.</summary>
    LowRisk = 3
}
