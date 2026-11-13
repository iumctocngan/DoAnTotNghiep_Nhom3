"""
Context Retriever — Gom dữ liệu từ 3 nguồn để tiêm vào LLM prompt.
🚧 Tuần 1: Scaffold.
    Tuần 3: Hoàn thiện toàn bộ SQL queries và xử lý missing data.

Guardrails:
- Deterministic SQL Retrieval — luôn tìm theo StudentId chính xác.
- Nếu không tìm thấy học sinh → raise exception, không suy đoán.
- Nếu thiếu dữ liệu → thêm vào missing_data_notes, không bịa số liệu.
- Read-only — không thực hiện ghi vào DB.
"""
import json
from datetime import date, datetime, timezone

from sqlalchemy import text
from sqlalchemy.orm import Session

from ai_service.schemas.cskh_schemas import (
    AttendanceSummary,
    FinancialSummary,
    Module1Assessment,
    RenewalPrediction,
    StudentContext,
)


def _as_date(value):
    if isinstance(value, datetime):
        return value.date()
    if isinstance(value, str):
        return date.fromisoformat(value)
    return value


def _safe_drivers(raw: str | None) -> list[dict]:
    try:
        parsed = json.loads(raw or "[]")
        return parsed if isinstance(parsed, list) else []
    except json.JSONDecodeError:
        return []


def retrieve_student_context(db: Session, student_id: int) -> StudentContext:
    """
    Gom đầy đủ context của học sinh từ:
    1. Core Platform: Students, Guardians, Enrollments, Attendances, Invoices, Payments
    2. Module 1: Assessment (điểm và nhận xét đã duyệt), StudentRemarks (nhận xét cũ)
    3. Module 3: StudentRenewalPredictions (dự đoán AI gần nhất)

    Returns:
        StudentContext — JSON-serializable, sẵn sàng inject vào System Prompt.

    Raises:
        ValueError: Nếu không tìm thấy student_id trong database.
    """
    student = db.execute(text("""
        SELECT Id, StudentCode, FullName FROM Students WHERE Id = :student_id
    """), {"student_id": student_id}).mappings().first()
    if student is None:
        raise ValueError("Không tìm thấy dữ liệu học viên")

    missing: list[str] = []
    enrollment = db.execute(text("""
        SELECT TOP 1 e.Id, c.Name AS ClassName, l.Name AS LevelName, u.FullName AS TeacherName
        FROM Enrollments e
        INNER JOIN Classes c ON c.Id = e.ClassId
        INNER JOIN Levels l ON l.Id = c.LevelId
        LEFT JOIN AspNetUsers u ON u.Id = c.MainTeacherUserId
        WHERE e.StudentId = :student_id AND e.Status IN (1, 2)
        ORDER BY CASE WHEN e.Status = 1 THEN 0 ELSE 1 END, e.StartDate DESC, e.Id DESC
    """), {"student_id": student_id}).mappings().first()
    if enrollment is None:
        missing.append("Chưa có ghi danh Active hoặc Paused để xác định lớp hiện tại")
    elif not enrollment["TeacherName"]:
        missing.append("Chưa có dữ liệu giáo viên phụ trách lớp hiện tại")

    guardian = db.execute(text("""
        SELECT TOP 1 g.FullName, g.Phone
        FROM StudentGuardians sg INNER JOIN Guardians g ON g.Id = sg.GuardianId
        WHERE sg.StudentId = :student_id AND sg.IsPrimary = 1 AND g.IsActive = 1
    """), {"student_id": student_id}).mappings().first()
    if guardian is None:
        missing.append("Chưa có dữ liệu người giám hộ chính")

    enrollment_id = enrollment["Id"] if enrollment else None
    attendance_row = None
    recent_statuses = []
    if enrollment_id is not None:
        attendance_row = db.execute(text("""
            SELECT COUNT(a.Id) AS TotalCount,
                   SUM(CASE WHEN a.Status = 1 THEN 1 ELSE 0 END) AS PresentCount
            FROM Attendances a INNER JOIN Sessions s ON s.Id = a.SessionId
            WHERE a.EnrollmentId = :enrollment_id
              AND s.SessionDate >= DATEADD(day, -60, CAST(GETUTCDATE() AS date))
              AND s.Status <> 3
        """), {"enrollment_id": enrollment_id}).mappings().first()
        recent_statuses = db.execute(text("""
            SELECT TOP 5 a.Status FROM Attendances a INNER JOIN Sessions s ON s.Id = a.SessionId
            WHERE a.EnrollmentId = :enrollment_id AND s.Status <> 3
            ORDER BY s.SessionDate DESC, s.Id DESC
        """), {"enrollment_id": enrollment_id}).scalars().all()
    total = int(attendance_row["TotalCount"] or 0) if attendance_row else 0
    if total == 0:
        missing.append("Chưa có dữ liệu điểm danh trong 60 ngày gần nhất")
    consecutive_absent = 0
    for attendance_status in recent_statuses:
        if attendance_status != 2:
            break
        consecutive_absent += 1
    attendance = AttendanceSummary(
        attendance_rate_last_60d=(round(float(attendance_row["PresentCount"] or 0) / total, 4) if total else None),
        consecutive_absent_count=(consecutive_absent if recent_statuses else None),
        recent_5_absences=(sum(1 for item in recent_statuses if item == 2) if recent_statuses else None),
    )

    finance = None
    if enrollment_id is not None:
        finance = db.execute(text("""
            SELECT SUM(i.AmountDue - ISNULL(p.PaidAmount, 0)) AS CurrentDebt,
                   MIN(i.DueDate) AS DueDate, MAX(i.PeriodEnd) AS PeriodEnd
            FROM Invoices i
            OUTER APPLY (SELECT SUM(Amount) AS PaidAmount FROM Payments
                         WHERE InvoiceId = i.Id AND Status = 1) p
            WHERE i.EnrollmentId = :enrollment_id AND i.Status <> 2
        """), {"enrollment_id": enrollment_id}).mappings().first()
    current_debt = float(finance["CurrentDebt"]) if finance and finance["CurrentDebt"] is not None else None
    due_date = _as_date(finance["DueDate"]) if finance and finance["DueDate"] else None
    period_end = _as_date(finance["PeriodEnd"]) if finance and finance["PeriodEnd"] else None
    if current_debt is None:
        missing.append("Chưa có hóa đơn hợp lệ để xác định công nợ")
    today = date.today()
    financial = FinancialSummary(
        current_debt=current_debt,
        due_date=due_date,
        days_until_period_end=max(0, (period_end - today).days) if period_end else None,
        has_overdue=(current_debt > 0 and due_date < today) if current_debt is not None and due_date else None,
    )

    nhan_xet = None
    if enrollment_id is not None:
        nhan_xet = db.execute(text("""
            SELECT TOP 1 Feedback AS Content, Score
            FROM Assessment
            WHERE EnrollmentId = :enrollment_id AND ApprovedAt IS NOT NULL
              AND NULLIF(LTRIM(RTRIM(Feedback)), N'') IS NOT NULL
            ORDER BY ApprovedAt DESC, Id DESC
        """), {"enrollment_id": enrollment_id}).mappings().first()
        if nhan_xet is None:
            nhan_xet = db.execute(text("""
                SELECT TOP 1 Content, NULL AS Score
                FROM StudentRemarks WHERE EnrollmentId = :enrollment_id
                ORDER BY CreatedAt DESC, Id DESC
            """), {"enrollment_id": enrollment_id}).mappings().first()
    if nhan_xet is None:
        missing.append("Chưa có nhận xét giáo viên gần nhất")
    if nhan_xet is None or nhan_xet["Score"] is None:
        missing.append("Chưa có điểm bài đánh giá đã duyệt")
    assessment = Module1Assessment(
        teacher_note=nhan_xet["Content"] if nhan_xet else None,
        recent_score=float(nhan_xet["Score"]) if nhan_xet and nhan_xet["Score"] is not None else None,
    )

    prediction = None
    if enrollment_id is not None:
        prediction = db.execute(text("""
            SELECT TOP 1 RenewalProbability, RiskLevel, TopDriversJson, CreatedAt
            FROM StudentRenewalPredictions
            WHERE StudentId = :student_id AND EnrollmentId = :enrollment_id
            ORDER BY PredictionDate DESC, CreatedAt DESC, Id DESC
        """), {"student_id": student_id, "enrollment_id": enrollment_id}).mappings().first()
    risk_labels = {1: "HighRisk", 2: "MediumRisk", 3: "LowRisk"}
    if prediction is None:
        missing.append("Chưa có dự đoán tái phí mới nhất")
    renewal_prediction = RenewalPrediction(
        renewal_probability=float(prediction["RenewalProbability"]) if prediction else None,
        risk_level=risk_labels.get(prediction["RiskLevel"]) if prediction else None,
        top_drivers=_safe_drivers(prediction["TopDriversJson"]) if prediction else [],
        predicted_at=prediction["CreatedAt"] if prediction else None,
    )

    return StudentContext(
        student_id=int(student["Id"]), student_name=student["FullName"], student_code=student["StudentCode"],
        class_name=enrollment["ClassName"] if enrollment else None,
        level_name=enrollment["LevelName"] if enrollment else None,
        teacher_name=enrollment["TeacherName"] if enrollment else None,
        primary_guardian_name=guardian["FullName"] if guardian else None,
        primary_guardian_phone=guardian["Phone"] if guardian else None,
        attendance=attendance, financial=financial, assessment=assessment,
        renewal_prediction=renewal_prediction, retrieved_at=datetime.now(timezone.utc),
        data_is_complete=not missing, missing_data_notes=missing,
    )
