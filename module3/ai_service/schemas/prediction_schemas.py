"""
Pydantic schemas cho ML Renewal Prediction API.
Dùng để validate request/response — không phải SQLAlchemy model.
"""
from datetime import date, datetime
from pydantic import BaseModel, Field


class PredictStudentRequest(BaseModel):
    """Request on-demand dự đoán 1 học sinh cụ thể."""
    student_id: int = Field(..., description="ID học sinh cần dự đoán")
    enrollment_id: int = Field(..., description="ID enrollment đang active")


class ShapDriver(BaseModel):
    """Một yếu tố SHAP giải thích nguyên nhân rủi ro."""
    feature: str = Field(..., description="Tên feature (ví dụ: attendance_rate_last_60d)")
    direction: str = Field(..., description="'positive' hoặc 'negative'")
    impact: float = Field(..., description="Giá trị SHAP (|impact| càng lớn càng quan trọng)")
    description: str = Field(..., description="Mô tả tiếng Việt dễ hiểu cho nhân viên CSKH")


class PredictionResult(BaseModel):
    """Kết quả dự đoán tái phí cho 1 học sinh."""
    student_id: int
    enrollment_id: int
    renewal_probability: float = Field(..., ge=0.0, le=1.0, description="Xác suất tái phí [0.0, 1.0]")
    risk_level: str = Field(..., description="'HighRisk', 'MediumRisk', hoặc 'LowRisk'")
    days_until_period_end: int
    top_drivers: list[ShapDriver] = Field(default_factory=list, description="Top 3 SHAP drivers")
    model_version: str
    prediction_date: date


class BatchPredictResponse(BaseModel):
    """Kết quả sau khi chạy batch dự đoán."""
    total_processed: int
    high_risk_count: int
    medium_risk_count: int
    low_risk_count: int
    results: list[PredictionResult]
    ran_at: datetime
