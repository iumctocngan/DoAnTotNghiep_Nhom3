using CmsEdu.Domain.Common;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Domain.Entities;

/// <summary>
/// Kết quả dự đoán khả năng tái phí của học sinh từ mô hình Machine Learning.
/// Mỗi lần chạy batch hoặc on-demand tạo 1 snapshot riêng biệt, không ghi đè lịch sử.
/// </summary>
public class StudentRenewalPrediction : BaseEntity
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;

    /// <summary>Ngày thực hiện dự đoán (UTC).</summary>
    public DateOnly PredictionDate { get; set; }

    /// <summary>Xác suất tái phí [0.0, 1.0]. Ví dụ: 0.92 = 92%.</summary>
    public double RenewalProbability { get; set; }

    /// <summary>Mức độ rủi ro: 1=HighRisk (<50%), 2=MediumRisk (50–74%), 3=LowRisk (>=75%).</summary>
    public RenewalRiskLevel RiskLevel { get; set; }

    /// <summary>Số ngày còn lại đến khi kết thúc kỳ học phí hiện tại.</summary>
    public int DaysUntilPeriodEnd { get; set; }

    /// <summary>JSON chứa Top 3 SHAP drivers giải thích nguyên nhân rủi ro.</summary>
    public string TopDriversJson { get; set; } = string.Empty;

    /// <summary>Phiên bản model ML đã sử dụng. Ví dụ: "v1.0.0-lgbm".</summary>
    public string ModelVersion { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
