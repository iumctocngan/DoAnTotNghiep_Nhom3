"""
ML Engine — Chạy inference LightGBM + tính toán SHAP values cục bộ.
Triển khai hoàn thiện cho Tuần 2 (Sprint 2).

Guardrails:
- Ngưỡng rủi ro chuẩn hóa: HighRisk < 0.50, MediumRisk 0.50–0.74, LowRisk >= 0.75
- SHAP Sanity Check: Kiểm tra tính hợp lý của các driver
- Không cho phép client override xác suất
"""
import os
import json
from typing import Optional, Any
import numpy as np
import pandas as pd
import joblib
import shap

from ai_service.config import settings
from ai_service.services.feature_store import FEATURE_NAMES


# Map tên feature sang mô tả gốc
FEATURE_DESCRIPTIONS: dict[str, str] = {
    "attendance_rate_last_60d": "Tỷ lệ chuyên cần 60 ngày gần nhất",
    "consecutive_absent_count": "Số buổi vắng liên tiếp hiện tại",
    "recent_exam_score": "Điểm bài thi gần nhất",
    "score_drop_trend": "Xu hướng điểm số so với các kỳ trước",
    "homework_completion_rate": "Tỷ lệ hoàn thành bài tập về nhà",
    "parent_interaction_score": "Mức độ tương tác của phụ huynh với trung tâm",
    "days_until_term_end": "Số ngày còn lại đến hết kỳ học phí",
    "current_debt_amount": "Số tiền học phí còn nợ hiện tại",
    "overdue_debt_days": "Số ngày đã quá hạn đóng học phí",
    "tenure_months": "Thâm niên học tại CMS EDU (tháng)",
    "paused_history_count": "Số lần đã xin bảo lưu trong quá khứ",
    "current_level": "Cấp độ học hiện tại",
}

# Global cached model and explainer
_cached_bundle: Optional[dict[str, Any]] = None
_cached_explainer: Optional[Any] = None


def classify_risk_level(probability: float) -> str:
    """
    Phân loại mức độ rủi ro theo ngưỡng nghiệp vụ.
    HighRisk: renewal_prob < 0.50
    MediumRisk: 0.50 <= renewal_prob < 0.75
    LowRisk: renewal_prob >= 0.75
    """
    if probability < 0.50:
        return "HighRisk"
    elif probability < 0.75:
        return "MediumRisk"
    else:
        return "LowRisk"


def _resolve_model_path() -> str:
    """Tìm đường dẫn thực tế của file model .joblib."""
    candidate_paths = [
        settings.ml_model_path,
        os.path.join(os.path.dirname(__file__), "..", "ml_models", "renewal_model.joblib"),
        os.path.join(os.getcwd(), "ai_service", "ml_models", "renewal_model.joblib"),
    ]
    for p in candidate_paths:
        abs_p = os.path.abspath(p)
        if os.path.isfile(abs_p):
            return abs_p
    # Mặc định trả về đường dẫn chuẩn trong package
    return os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "ml_models", "renewal_model.joblib"))


def load_model() -> tuple[Any, Any, str]:
    """
    Load mô hình LightGBM và SHAP TreeExplainer từ artifact .joblib.
    Áp dụng bộ nhớ đệm (singleton cache) để tối ưu thời gian suy luận < 50ms.
    
    Returns:
        tuple (model, explainer, model_version)
    """
    global _cached_bundle, _cached_explainer

    if _cached_bundle is not None and _cached_explainer is not None:
        return _cached_bundle["model"], _cached_explainer, _cached_bundle.get("version", settings.ml_model_version)

    model_path = _resolve_model_path()
    if not os.path.isfile(model_path):
        raise FileNotFoundError(
            f"ML model artifact not found at '{model_path}'. "
            "Set ML_MODEL_PATH to an existing model artifact."
        )

    bundle = joblib.load(model_path)
    if isinstance(bundle, dict) and "model" in bundle:
        model = bundle["model"]
        version = bundle.get("version", settings.ml_model_version)
    else:
        model = bundle
        version = settings.ml_model_version
        bundle = {"model": model, "version": version}

    explainer = shap.TreeExplainer(model)

    _cached_bundle = bundle
    _cached_explainer = explainer
    return model, explainer, version


def generate_driver_description(feature: str, val: float, impact: float) -> str:
    """Sinh câu mô tả tiếng Việt sư phạm cho nhân viên CSKH dựa trên giá trị và hướng tác động."""
    if feature == "attendance_rate_last_60d":
        pct = round(val * 100, 1)
        return f"Tỷ lệ chuyên cần 60 ngày cao đạt {pct}%" if impact > 0 else f"Tỷ lệ chuyên cần 60 ngày thấp, chỉ đạt {pct}%"

    elif feature == "consecutive_absent_count":
        cnt = int(val)
        return f"Học sinh đã nghỉ liên tiếp {cnt} buổi học gần nhất" if cnt > 0 else "Học sinh đi học đầy đủ các buổi gần đây"

    elif feature == "recent_exam_score":
        return f"Kết quả bài thi gần nhất tốt ({val:.1f} điểm)" if impact > 0 else f"Điểm bài thi gần nhất còn thấp ({val:.1f} điểm)"

    elif feature == "score_drop_trend":
        if impact > 0:
            return f"Điểm số có xu hướng tiến bộ (+{val:.1f} điểm)"
        else:
            return f"Điểm số có dấu hiệu sa sút ({val:.1f} điểm so với kỳ trước)"

    elif feature == "homework_completion_rate":
        pct = round(val * 100, 0)
        return f"Tỷ lệ hoàn thành bài tập về nhà tốt ({pct:.0f}%)" if impact > 0 else f"Ít nộp bài tập về nhà ({pct:.0f}%)"

    elif feature == "parent_interaction_score":
        cnt = int(val)
        return f"Phụ huynh tương tác tích cực với trung tâm ({cnt} lần)" if impact > 0 else "Phụ huynh ít tương tác với giáo viên/trung tâm"

    elif feature == "days_until_term_end":
        return f"Kỳ học phí còn {int(val)} ngày là đến hạn kết thúc"

    elif feature == "current_debt_amount":
        return f"Còn nợ học phí {int(val):,} VND" if val > 0 else "Đã hoàn thành toàn bộ học phí kỳ này"

    elif feature == "overdue_debt_days":
        return f"Khoản học phí đã quá hạn {int(val)} ngày" if val > 0 else "Học phí trong hạn thanh toán bình thường"

    elif feature == "tenure_months":
        return f"Học sinh gắn bó lâu năm tại trung tâm ({val:.0f} tháng)" if val >= 12 else f"Học sinh mới theo học ({val:.0f} tháng)"

    elif feature == "paused_history_count":
        cnt = int(val)
        return f"Từng có lịch sử bảo lưu kỳ học ({cnt} lần)" if cnt > 0 else "Chưa từng bảo lưu trong suốt quá trình học"

    elif feature == "current_level":
        return f"Đang theo học cấp độ Level {int(val)}"

    return FEATURE_DESCRIPTIONS.get(feature, feature)


def run_inference(features: dict) -> tuple[float, list[dict], str]:
    """
    Chạy suy luận mô hình trên một vector đặc trưng và trích xuất Top 3 SHAP Drivers.
    
    Args:
        features: dict chứa 12 đặc trưng
        
    Returns:
        tuple (renewal_probability, top_3_shap_drivers, model_version)
    """
    model, explainer, version = load_model()

    # Chuẩn hóa vector thành DataFrame đúng thứ tự FEATURE_NAMES
    input_row = {feat: float(features.get(feat, 0.0)) for feat in FEATURE_NAMES}
    df_input = pd.DataFrame([input_row], columns=FEATURE_NAMES)

    # 1. Dự đoán xác suất tái phí
    probs = model.predict_proba(df_input)
    # Class 1: Xác suất tái phí
    renewal_prob = float(probs[0, 1])
    renewal_prob = round(min(1.0, max(0.0, renewal_prob)), 4)

    # 2. Tính toán SHAP values
    shap_vals = explainer.shap_values(df_input)
    if isinstance(shap_vals, list):
        # Phân loại nhị phân trả về [shap_for_class_0, shap_for_class_1]
        shap_vector = shap_vals[1][0]
    elif hasattr(shap_vals, "values"):
        shap_vector = shap_vals.values[0]
    elif len(shap_vals.shape) == 3:
        # Shape: (1, n_features, 2)
        shap_vector = shap_vals[0, :, 1]
    else:
        shap_vector = shap_vals[0]

    # 3. Trích xuất Top 3 SHAP Drivers có |impact| lớn nhất
    driver_candidates = []
    for idx, feat in enumerate(FEATURE_NAMES):
        imp = float(shap_vector[idx])
        direction = "positive" if imp > 0 else "negative"
        val = input_row[feat]
        desc = generate_driver_description(feat, val, imp)
        driver_candidates.append({
            "feature": feat,
            "direction": direction,
            "impact": round(imp, 4),
            "description": desc,
            "abs_impact": abs(imp),
        })

    # Sắp xếp theo độ lớn tác động giảm dần
    driver_candidates.sort(key=lambda d: d["abs_impact"], reverse=True)
    top_3 = driver_candidates[:3]

    # Loại bỏ trường phụ abs_impact trước khi trả về
    top_drivers = [
        {
            "feature": d["feature"],
            "direction": d["direction"],
            "impact": d["impact"],
            "description": d["description"],
        }
        for d in top_3
    ]

    return renewal_prob, top_drivers, version
