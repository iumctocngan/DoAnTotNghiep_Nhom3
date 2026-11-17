"""
Integration tests cho Prediction Router (Sprint 2 - TC-ML-06, TC-ML-07).
"""
from datetime import datetime, timedelta, timezone

import jwt
import pytest
from starlette.testclient import TestClient
from unittest.mock import patch, MagicMock

import ai_service.security as security
from ai_service.main import app
from ai_service.database import get_db

client = TestClient(app)
JWT_ISSUER = "cms-edu-test"
JWT_AUDIENCE = "cms-edu-ai-test"
JWT_SIGNING_KEY = "test-signing-key-with-at-least-thirty-two-bytes"


@pytest.fixture
def admin_headers(monkeypatch):
    monkeypatch.setattr(security.settings, "jwt_issuer", JWT_ISSUER)
    monkeypatch.setattr(security.settings, "jwt_audience", JWT_AUDIENCE)
    monkeypatch.setattr(security.settings, "jwt_signing_key", JWT_SIGNING_KEY)
    token = jwt.encode(
        {
            "sub": "test-admin",
            "role": "Admin",
            "iss": JWT_ISSUER,
            "aud": JWT_AUDIENCE,
            "exp": datetime.now(timezone.utc) + timedelta(minutes=5),
        },
        JWT_SIGNING_KEY,
        algorithm="HS256",
    )
    return {"Authorization": f"Bearer {token}"}


def test_health_check():
    """Kiểm tra health check service."""
    response = client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "ok"


@pytest.mark.parametrize(
    "path",
    ["/api/ml/predict-student", "/api/ml/predict-batch"],
)
def test_prediction_endpoints_require_bearer_token(path):
    response = client.post(path, json={"student_id": 105, "enrollment_id": 42})

    assert response.status_code == 401


def test_predict_student_endpoint(admin_headers):
    """Kiểm tra endpoint on-demand POST /api/ml/predict-student."""
    mock_db = MagicMock()
    # Override get_db dependency
    app.dependency_overrides[get_db] = lambda: mock_db

    try:
        response = client.post(
            "/api/ml/predict-student",
            json={"student_id": 105, "enrollment_id": 42},
            headers=admin_headers,
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


def test_predict_batch_endpoint(admin_headers):
    """Kiểm tra endpoint batch POST /api/ml/predict-batch."""
    mock_db = MagicMock()
    app.dependency_overrides[get_db] = lambda: mock_db

    try:
        with patch("ai_service.services.feature_store.get_students_for_batch_prediction") as mock_batch_students:
            mock_batch_students.return_value = [
                {"student_id": 101, "enrollment_id": 11},
                {"student_id": 102, "enrollment_id": 12},
            ]

            response = client.post("/api/ml/predict-batch", headers=admin_headers)
            assert response.status_code == 200
            data = response.json()

            assert data["total_processed"] == 2
            assert data["high_risk_count"] + data["medium_risk_count"] + data["low_risk_count"] == 2
            assert len(data["results"]) == 2
            assert "ran_at" in data
    finally:
        app.dependency_overrides.clear()
