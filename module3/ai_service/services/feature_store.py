"""
Feature Store — trích xuất 12 features từ SQL Server cho ML Renewal Engine.
Triển khai hoàn thiện cho Tuần 2 (Sprint 2).

Guardrails:
- Temporal Integrity: Không dùng dữ liệu của kỳ tương lai (No Data Leakage).
- Zero-Crash: Missing values được điền bằng giá trị trung bình / mặc định hợp lệ.
- Bắt buộc trả về đúng 12 features theo thứ tự quy ước.
"""
import re
from datetime import date, datetime
from typing import Optional
from sqlalchemy.orm import Session
from sqlalchemy import text


FEATURE_NAMES = [
    "attendance_rate_last_60d",
    "consecutive_absent_count",
    "recent_exam_score",
    "score_drop_trend",
    "homework_completion_rate",
    "parent_interaction_score",
    "days_until_term_end",
    "current_debt_amount",
    "overdue_debt_days",
    "tenure_months",
    "paused_history_count",
    "current_level",
]

FEATURE_DEFAULTS: dict[str, float] = {
    "attendance_rate_last_60d": 0.85,
    "consecutive_absent_count": 0.0,
    "recent_exam_score": 7.5,
    "score_drop_trend": 0.0,
    "homework_completion_rate": 0.80,
    "parent_interaction_score": 1.0,
    "days_until_term_end": 30.0,
    "current_debt_amount": 0.0,
    "overdue_debt_days": 0.0,
    "tenure_months": 1.0,
    "paused_history_count": 0.0,
    "current_level": 1.0,
}


def extract_features_for_student(db: Session, student_id: int, enrollment_id: int) -> dict[str, float]:
    """
    Trích xuất vector 12 đặc trưng cho học sinh từ SQL Server.
    
    Args:
        db: SQLAlchemy Session kết nối tới Shared DB
        student_id: Mã định danh học sinh
        enrollment_id: Mã kỳ ghi danh đang hoạt động

    Returns:
        dict gồm đúng 12 keys tương ứng 12 features được chuẩn hóa số học.
    """
    features: dict[str, float] = dict(FEATURE_DEFAULTS)

    try:
        # 1. Trích xuất chuyên cần 60 ngày gần nhất (Attendance Rate)
        att_query = text("""
            SELECT 
                COUNT(CASE WHEN a.Status = 1 THEN 1 END) AS PresentCount,
                COUNT(a.Id) AS TotalCount
            FROM Attendances a
            INNER JOIN Sessions s ON a.SessionId = s.Id
            WHERE a.EnrollmentId = :enrollment_id
              AND s.SessionDate >= DATEADD(day, -60, CAST(GETUTCDATE() AS DATE))
        """)
        att_row = db.execute(att_query, {"enrollment_id": enrollment_id}).mappings().first()
        if att_row and att_row["TotalCount"] and att_row["TotalCount"] > 0:
            features["attendance_rate_last_60d"] = round(float(att_row["PresentCount"]) / float(att_row["TotalCount"]), 4)

        # 2. Trích xuất số buổi nghỉ liên tiếp gần nhất (Consecutive Absent Sessions)
        consec_query = text("""
            SELECT a.Status
            FROM Attendances a
            INNER JOIN Sessions s ON a.SessionId = s.Id
            WHERE a.EnrollmentId = :enrollment_id
            ORDER BY s.SessionDate DESC
        """)
        consec_rows = db.execute(consec_query, {"enrollment_id": enrollment_id}).scalars().all()
        consecutive_absents = 0
        for status in consec_rows:
            # 2 = Absent trong AttendanceStatus enum
            if status == 2:
                consecutive_absents += 1
            else:
                break
        features["consecutive_absent_count"] = float(consecutive_absents)

        # 3 & 4. Trích xuất Điểm thi & Xu hướng điểm từ Module 1 (StudentRemarks)
        remark_query = text("""
            SELECT Content, CreatedAt
            FROM StudentRemarks
            WHERE EnrollmentId = :enrollment_id
            ORDER BY CreatedAt DESC
        """)
        remark_rows = db.execute(remark_query, {"enrollment_id": enrollment_id}).mappings().all()
        extracted_scores: list[float] = []
        for r in remark_rows:
            content = r.get("Content") or ""
            # Tìm kiếm điểm số trong nhận xét (ví dụ: "Điểm: 8.5", "Score 9", "7.0 điểm")
            matches = re.findall(r"(?:điểm|score|đạt)\s*[:=]?\s*(\d+(?:\.\d+)?)", content, re.IGNORECASE)
            for m in matches:
                try:
                    val = float(m)
                    if 0.0 <= val <= 10.0:
                        extracted_scores.append(val)
                except ValueError:
                    pass

        if extracted_scores:
            features["recent_exam_score"] = round(extracted_scores[0], 2)
            if len(extracted_scores) > 1:
                prev_avg = sum(extracted_scores[1:4]) / len(extracted_scores[1:4])
                features["score_drop_trend"] = round(extracted_scores[0] - prev_avg, 2)

        # 5. Tỷ lệ hoàn thành bài tập về nhà (mặc định 0.80 hoặc theo điểm danh chuyên cần)
        features["homework_completion_rate"] = round(min(1.0, max(0.0, features["attendance_rate_last_60d"] * 0.95)), 2)

        # 6. Mức độ tương tác của phụ huynh (CskhInteractions)
        interact_query = text("""
            SELECT COUNT(Id) AS TotalInteractions
            FROM CskhInteractions
            WHERE StudentId = :student_id
        """)
        interact_count = db.execute(interact_query, {"student_id": student_id}).scalar()
        if interact_count is not None and interact_count > 0:
            features["parent_interaction_score"] = float(interact_count)

        # 7, 8, 9. Dữ liệu Tài chính: Days until term end, Debt amount, Overdue debt days
        fin_query = text("""
            SELECT 
                i.AmountDue,
                i.DueDate,
                i.PeriodEnd,
                ISNULL(SUM(p.Amount), 0) AS PaidAmount
            FROM Invoices i
            LEFT JOIN Payments p ON i.Id = p.InvoiceId AND p.Status = 1
            WHERE i.EnrollmentId = :enrollment_id AND i.Status != 4
            GROUP BY i.Id, i.AmountDue, i.DueDate, i.PeriodEnd
        """)
        fin_row = db.execute(fin_query, {"enrollment_id": enrollment_id}).mappings().first()
        today = date.today()
        if fin_row:
            period_end = fin_row.get("PeriodEnd")
            due_date = fin_row.get("DueDate")
            amount_due = float(fin_row.get("AmountDue") or 0.0)
            paid_amount = float(fin_row.get("PaidAmount") or 0.0)
            debt = max(0.0, amount_due - paid_amount)

            features["current_debt_amount"] = round(debt, 2)
            if period_end:
                if isinstance(period_end, str):
                    period_end = date.fromisoformat(period_end)
                elif isinstance(period_end, datetime):
                    period_end = period_end.date()
                features["days_until_term_end"] = float(max(0, (period_end - today).days))

            if debt > 0.0 and due_date:
                if isinstance(due_date, str):
                    due_date = date.fromisoformat(due_date)
                elif isinstance(due_date, datetime):
                    due_date = due_date.date()
                overdue = (today - due_date).days
                features["overdue_debt_days"] = float(max(0, overdue))

        # 10, 11, 12. Lịch sử ghi danh & Cấp độ: Tenure months, Paused history, Current level
        enroll_query = text("""
            SELECT 
                e.StartDate,
                e.Status,
                e.PauseReason,
                c.LevelId
            FROM Enrollments e
            LEFT JOIN Classes c ON e.ClassId = c.Id
            WHERE e.StudentId = :student_id
            ORDER BY e.StartDate ASC
        """)
        enroll_rows = db.execute(enroll_query, {"student_id": student_id}).mappings().all()
        if enroll_rows:
            first_start = enroll_rows[0].get("StartDate")
            if first_start:
                if isinstance(first_start, str):
                    first_start = date.fromisoformat(first_start)
                elif isinstance(first_start, datetime):
                    first_start = first_start.date()
                tenure_days = max(1, (today - first_start).days)
                features["tenure_months"] = round(tenure_days / 30.0, 1)

            paused_count = sum(1 for e in enroll_rows if e.get("Status") == 2 or e.get("PauseReason"))
            features["paused_history_count"] = float(paused_count)

            latest_level = enroll_rows[-1].get("LevelId")
            if latest_level is not None:
                features["current_level"] = float(latest_level)

    except Exception:
        # Trong trường hợp bảng chưa có dữ liệu hoặc DB connection fallback
        # giữ nguyên các giá trị feature an toàn theo dictionary mặc định
        pass

    # Đảm bảo không có giá trị None hoặc NaN
    for k in FEATURE_NAMES:
        if features.get(k) is None:
            features[k] = FEATURE_DEFAULTS[k]

    return features


def get_students_for_batch_prediction(db: Session) -> list[dict]:
    """
    Lấy danh sách học sinh đủ điều kiện chạy batch prediction:
    - Enrollment đang Active (Status = 1)
    - Kỳ học phí đang ở tháng thứ 4–5 (còn 30–60 ngày là hết hạn PeriodEnd)
    Nếu không tìm thấy học sinh trong khoảng 30-60 ngày, fallback lấy tất cả học sinh đang Active.
    """
    try:
        # Truy vấn ưu tiên: học sinh có Invoice còn 30-60 ngày hết hạn
        query = text("""
            SELECT DISTINCT e.StudentId, e.Id AS EnrollmentId
            FROM Enrollments e
            INNER JOIN Invoices i ON e.Id = i.EnrollmentId
            WHERE e.Status = 1 AND i.Status != 4
              AND DATEDIFF(day, CAST(GETUTCDATE() AS DATE), i.PeriodEnd) BETWEEN 30 AND 60
        """)
        rows = db.execute(query).mappings().all()
        if rows:
            return [{"student_id": int(r["StudentId"]), "enrollment_id": int(r["EnrollmentId"])} for r in rows]

        # Fallback: Lấy toàn bộ học sinh có Enrollment Active
        fallback_query = text("""
            SELECT DISTINCT e.StudentId, e.Id AS EnrollmentId
            FROM Enrollments e
            WHERE e.Status = 1
        """)
        fallback_rows = db.execute(fallback_query).mappings().all()
        return [{"student_id": int(r["StudentId"]), "enrollment_id": int(r["EnrollmentId"])} for r in fallback_rows]
    except Exception:
        return []
