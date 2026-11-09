"""
Unit tests cho Feature Store (Sprint 2 - TC-ML-01, TC-ML-02).
"""
import pytest
from unittest.mock import MagicMock
from ai_service.services.feature_store import (
    FEATURE_NAMES,
    FEATURE_DEFAULTS,
    extract_features_for_student,
    get_students_for_batch_prediction,
)


def test_feature_names_count_and_keys():
    """Kiểm tra có đúng 12 features theo thiết kế hệ thống."""
    assert len(FEATURE_NAMES) == 12
    assert "attendance_rate_last_60d" in FEATURE_NAMES
    assert "consecutive_absent_count" in FEATURE_NAMES
    assert "recent_exam_score" in FEATURE_NAMES
    assert "score_drop_trend" in FEATURE_NAMES
    assert "homework_completion_rate" in FEATURE_NAMES
    assert "parent_interaction_score" in FEATURE_NAMES
    assert "days_until_term_end" in FEATURE_NAMES
    assert "current_debt_amount" in FEATURE_NAMES
    assert "overdue_debt_days" in FEATURE_NAMES
    assert "tenure_months" in FEATURE_NAMES
    assert "paused_history_count" in FEATURE_NAMES
    assert "current_level" in FEATURE_NAMES


def test_extract_features_fallback_when_db_empty():
    """Kiểm tra cơ chế Imputation & Zero-Crash khi DB trả về rỗng hoặc lỗi."""
    mock_db = MagicMock()
    # Mock execute trả về rỗng
    mock_db.execute.return_value.mappings.return_value.first.return_value = None
    mock_db.execute.return_value.scalars.return_value.all.return_value = []
    mock_db.execute.return_value.mappings.return_value.all.return_value = []
    mock_db.execute.return_value.scalar.return_value = None

    features = extract_features_for_student(mock_db, student_id=999, enrollment_id=888)

    assert isinstance(features, dict)
    assert len(features) == 12
    # Đảm bảo không có giá trị None
    for k in FEATURE_NAMES:
        assert k in features
        assert features[k] is not None
        assert isinstance(features[k], (int, float))


def test_get_students_for_batch_prediction_fallback():
    """Kiểm tra hàm batch student retrieval trả về danh sách hợp lệ."""
    mock_db = MagicMock()
    mock_db.execute.return_value.mappings.return_value.all.return_value = [
        {"StudentId": 101, "EnrollmentId": 201},
        {"StudentId": 102, "EnrollmentId": 202},
    ]

    students = get_students_for_batch_prediction(mock_db)
    assert len(students) == 2
    assert students[0]["student_id"] == 101
    assert students[0]["enrollment_id"] == 201
