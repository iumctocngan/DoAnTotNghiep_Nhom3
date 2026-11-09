"""
Integration tests cho Prediction Router (Sprint 2 - TC-ML-06, TC-ML-07).
"""
import pytest
from starlette.testclient import TestClient
from unittest.mock import patch, MagicMock

from ai_service.main import app
from ai_service.database import get_db

client = TestClient(app)


def test_health_check():
    """Kiểm tra health check service."""
    response = client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "ok"


def test_predict_student_endpoint():
    """Kiểm tra endpoint on-demand POST /api/ml/predict-student."""
    mock_db = MagicMock()
    # Override get_db dependency
    app.dependency_overrides[get_db] = lambda: mock_db

    try:
        response = client.post(
            "/api/ml/predict-student",
            json={"student_id": 105, "enrollment_id": 42},
        )
        assert response.status_code == 200
        data = response.json()

        assert data["student_id"] == 105
        assert data["enrollment_id"] == 42
        assert 0.0 <= data["renewal_probability"] <= 1.0
        assert data["risk_level"] in ["HighRisk", "MediumRisk", "LowRisk"]
        assert len(data["top_drivers"]) == 3
        assert "model_version" in data
        assert "prediction_date" in data

        for driver in data["top_drivers"]:
            assert "feature" in driver
            assert "direction" in driver
            assert "impact" in driver
            assert "description" in driver
    finally:
        app.dependency_overrides.clear()


def test_predict_batch_endpoint():
    """Kiểm tra endpoint batch POST /api/ml/predict-batch."""
    mock_db = MagicMock()
    app.dependency_overrides[get_db] = lambda: mock_db

    try:
        with patch("ai_service.services.feature_store.get_students_for_batch_prediction") as mock_batch_students:
            mock_batch_students.return_value = [
                {"student_id": 101, "enrollment_id": 11},
                {"student_id": 102, "enrollment_id": 12},
            ]

            response = client.post("/api/ml/predict-batch")
            assert response.status_code == 200
            data = response.json()

            assert data["total_processed"] == 2
            assert data["high_risk_count"] + data["medium_risk_count"] + data["low_risk_count"] == 2
            assert len(data["results"]) == 2
            assert "ran_at" in data
    finally:
        app.dependency_overrides.clear()
