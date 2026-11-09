"""
Pydantic schemas cho CSKH Context Retriever API.
Định nghĩa cấu trúc JSON context được tiêm vào LLM prompt — bất biến, fact-grounded.
"""
from datetime import date, datetime
from pydantic import BaseModel, Field


class AttendanceSummary(BaseModel):
    """Tóm tắt tình hình chuyên cần của học sinh."""
    attendance_rate_last_60d: float | None = Field(None, description="Tỷ lệ đi học 60 ngày gần nhất [0.0-1.0]")
    consecutive_absent_count: int | None = Field(None, description="Số buổi nghỉ liên tiếp hiện tại")
    recent_5_absences: int | None = Field(None, description="Số buổi vắng trong 5 buổi gần nhất")


class FinancialSummary(BaseModel):
    """Tóm tắt tình hình tài chính của học sinh."""
    current_debt: float | None = Field(None, description="Số tiền còn nợ hiện tại (VND)")
    days_until_period_end: int | None = Field(None, description="Số ngày đến hết kỳ học phí")
    due_date: date | None = None
    has_overdue: bool | None = None


class Module1Assessment(BaseModel):
    """Dữ liệu đánh giá học tập từ Module 1 (AI Assessment)."""
    recent_score: float | None = Field(None, description="Điểm bài thi gần nhất (thang 10)")
    score_trend: str | None = Field(None, description="'increasing', 'stable', hoặc 'declining'")
    teacher_note: str | None = Field(None, description="Nhận xét giáo viên phụ trách")
    weak_warning_count: int = Field(0, description="Số lần cảnh báo học yếu trong kỳ")


class RenewalPrediction(BaseModel):
    """Kết quả dự đoán tái phí mới nhất từ ML Model."""
    renewal_probability: float | None = None
    risk_level: str | None = None
    top_drivers: list[dict] = Field(default_factory=list)
    predicted_at: datetime | None = None


class StudentContext(BaseModel):
    """
    Context đầy đủ của học sinh — được inject vào System Prompt của LLM.
    ⚠️ Tất cả trường đều lấy từ SQL Server thật, không tự suy diễn.
    """
    # Thông tin cơ bản
    student_id: int
    student_name: str
    student_code: str
    class_name: str | None = None
    level_name: str | None = None
    teacher_name: str | None = None

    # Phụ huynh chính
    primary_guardian_name: str | None = None
    primary_guardian_phone: str | None = None

    # Dữ liệu nghiệp vụ
    attendance: AttendanceSummary
    financial: FinancialSummary
    assessment: Module1Assessment
    renewal_prediction: RenewalPrediction

    # Metadata
    retrieved_at: datetime = Field(default_factory=datetime.utcnow)
    data_is_complete: bool = Field(True, description="False nếu thiếu dữ liệu Module 1 hoặc dự đoán chưa chạy")
    missing_data_notes: list[str] = Field(
        default_factory=list,
        description="Danh sách các trường thiếu — chatbot phải thông báo 'Chưa có dữ liệu'"
    )


class GenerateCallScriptRequest(BaseModel):
    """Yêu cầu sinh tư vấn từ context đã truy xuất theo StudentId."""
    student_id: int = Field(..., gt=0)
    question: str = Field(..., min_length=1, max_length=1000)


class CallScriptResponse(BaseModel):
    summary: str
    risk_analysis: str
    action_plan: str
    suggested_call_script: list[str] = Field(min_length=5, max_length=5)
