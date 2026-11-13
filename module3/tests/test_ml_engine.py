"""
Unit tests cho ML Engine & SHAP Explainer (Sprint 2 - TC-ML-03, TC-ML-04, TC-ML-05).
"""
import time
import pytest
import ai_service.services.ml_engine as ml_engine
from ai_service.services.ml_engine import (
    load_model,
    classify_risk_level,
    run_inference,
    FEATURE_DESCRIPTIONS,
)


def test_classify_risk_level_thresholds():
    """Kiểm tra các ngưỡng rủi ro: HighRisk < 0.50, MediumRisk 0.50-0.74, LowRisk >= 0.75."""
    assert classify_risk_level(0.20) == "HighRisk"
    assert classify_risk_level(0.499) == "HighRisk"
    assert classify_risk_level(0.50) == "MediumRisk"
    assert classify_risk_level(0.70) == "MediumRisk"
    assert classify_risk_level(0.749) == "MediumRisk"
    assert classify_risk_level(0.75) == "LowRisk"
    assert classify_risk_level(0.95) == "LowRisk"


def test_load_model_requires_existing_artifact(monkeypatch, tmp_path):
    monkeypatch.setattr(ml_engine, "_cached_bundle", None)
    monkeypatch.setattr(ml_engine, "_cached_explainer", None)
    monkeypatch.setattr(
        ml_engine,
        "_resolve_model_path",
        lambda: str(tmp_path / "missing_model.joblib"),
    )

    with pytest.raises(FileNotFoundError, match="ML model artifact not found"):
        load_model()


def test_load_model_and_explainer():
    """Kiểm tra mô hình và TreeExplainer load thành công từ artifact."""
    model, explainer, version = load_model()
    assert model is not None
    assert explainer is not None
    assert isinstance(version, str)


def test_inference_on_high_risk_profile():
    """
    Kiểm tra học sinh có biểu hiện nguy cơ cao:
    - Vắng 3 buổi liên tiếp
    - Chuyên cần 50%
    - Điểm thi thấp và giảm mạnh
    - Nợ quá hạn
    Kết quả phải ra xác suất thấp và xếp loại HighRisk hoặc MediumRisk.
    """
    high_risk_features = {
        "attendance_rate_last_60d": 0.50,
        "consecutive_absent_count": 3.0,
        "recent_exam_score": 4.5,
        "score_drop_trend": -2.5,
        "homework_completion_rate": 0.40,
        "parent_interaction_score": 0.0,
        "days_until_term_end": 15.0,
        "current_debt_amount": 5000000.0,
        "overdue_debt_days": 20.0,
        "tenure_months": 2.0,
        "paused_history_count": 1.0,
        "current_level": 1.0,
    }

    prob, top_drivers, version = run_inference(high_risk_features)

    assert 0.0 <= prob <= 1.0
    assert prob < 0.50, f"Xác suất {prob} phải nhỏ hơn 0.50 cho học sinh nguy cơ cao"
    assert classify_risk_level(prob) == "HighRisk"
    assert len(top_drivers) == 3

    # Kiểm tra cấu trúc từng driver
    for d in top_drivers:
        assert "feature" in d
        assert d["direction"] in ["positive", "negative"]
        assert isinstance(d["impact"], float)
        assert isinstance(d["description"], str)
        assert len(d["description"]) > 0


def test_inference_on_low_risk_profile():
    """
    Kiểm tra học sinh tiềm năng cao:
    - Chuyên cần 98%
    - Không vắng buổi nào
    - Điểm 9.2, tăng tiến
    - Đã đóng đủ tiền
    - Gắn bó 18 tháng
    Kết quả phải ra xác suất cao và xếp loại LowRisk.
    """
    safe_features = {
        "attendance_rate_last_60d": 0.98,
        "consecutive_absent_count": 0.0,
        "recent_exam_score": 9.2,
        "score_drop_trend": 0.0,
        "homework_completion_rate": 0.95,
        "parent_interaction_score": 5.0,
        "days_until_term_end": 45.0,
        "current_debt_amount": 0.0,
        "overdue_debt_days": 0.0,
        "tenure_months": 18.0,
        "paused_history_count": 0.0,
        "current_level": 2.0,
    }

    prob, top_drivers, version = run_inference(safe_features)

    assert 0.0 <= prob <= 1.0
    assert prob >= 0.75, f"Xác suất {prob} phải >= 0.75 cho học sinh học tốt"
    assert classify_risk_level(prob) == "LowRisk"
    assert len(top_drivers) == 3


def test_inference_latency_sla():
    """Kiểm tra thời gian suy luận (SLA): phải hoàn thành trong < 50ms."""
    # Warm up
    sample = {
        "attendance_rate_last_60d": 0.85,
        "consecutive_absent_count": 0.0,
        "recent_exam_score": 7.5,
        "score_drop_trend": 0.0,
        "homework_completion_rate": 0.80,
        "parent_interaction_score": 1.0,
        "days_until_term_end": 30.0,
        "current_debt_amount": 0.0,
        "overdue_debt_days": 0.0,
        "tenure_months": 6.0,
        "paused_history_count": 0.0,
        "current_level": 1.0,
    }
    run_inference(sample)

    # Đo đạc thời gian
    start_time = time.perf_counter()
    run_inference(sample)
    duration_ms = (time.perf_counter() - start_time) * 1000.0

    print(f"\n[BENCHMARK] Inference latency: {duration_ms:.2f} ms")
    assert duration_ms < 50.0, f"Inference took {duration_ms:.2f} ms, vượt ngưỡng 50ms SLA"
