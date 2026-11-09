"""Kiểm thử chuyên sâu cho Bộ đệm & Tối ưu hóa Token Chatbot CSKH (Module 3).
Đảm bảo:
1. Deterministic Context Fingerprint: Đổi điểm hoặc đổi nợ là đổi khóa ngay (Zero Stale Data).
2. Chuẩn hóa câu hỏi & Cách ly phân quyền (Scope Isolation).
3. LRU Eviction & TTL Expiration.
4. Single-Flight Lock chống lãng phí token đồng thời (Cache Stampede).
5. Ước lượng và tích lũy số lượng token tiết kiệm được.
6. Context Compaction giảm kích thước prompt.
"""
import asyncio
from datetime import date, datetime
import pytest

from ai_service.schemas.cskh_schemas import (
    AttendanceSummary,
    CallScriptResponse,
    FinancialSummary,
    Module1Assessment,
    RenewalPrediction,
    StudentContext,
)
from ai_service.services.cskh_cache import (
    ChatbotResponseCache,
    compact_student_context,
    compute_context_fingerprint,
    estimate_tokens,
    generate_cache_key,
    normalize_question,
)


def _create_sample_context(debt: float = 150000.0, absent_count: int = 2, score: float = 8.5) -> StudentContext:
    return StudentContext(
        student_id=105,
        student_name="Nguyễn Văn Minh",
        student_code="HS0105",
        class_name="M-L2-01",
        level_name="Level 2",
        teacher_name="Trần Thị Mai",
        primary_guardian_name="Nguyễn Văn An",
        primary_guardian_phone="0912345678",
        attendance=AttendanceSummary(
            attendance_rate_last_60d=0.85,
            consecutive_absent_count=absent_count,
            recent_5_absences=1,
        ),
        financial=FinancialSummary(
            current_debt=debt,
            days_until_period_end=15,
            due_date=date(2026, 11, 20),
            has_overdue=False,
        ),
        assessment=Module1Assessment(
            recent_score=score,
            score_trend="stable",
            teacher_note="Tư duy tốt",
            weak_warning_count=0,
        ),
        renewal_prediction=RenewalPrediction(
            renewal_probability=0.72,
            risk_level="MediumRisk",
            top_drivers=[{"feature": "attendance", "description": "Vắng 2 buổi"}],
            predicted_at=datetime(2026, 10, 25, 8, 0, 0),
        ),
        retrieved_at=datetime.utcnow(),
        data_is_complete=True,
    )


def _create_sample_response() -> CallScriptResponse:
    return CallScriptResponse(
        summary="Học sinh Nguyễn Văn Minh có lực học tốt nhưng vắng 2 buổi liên tiếp.",
        risk_analysis="Nguy cơ rủi ro trung bình do vắng học gần đây.",
        action_plan="Trao đổi nhẹ nhàng với phụ huynh về lý do vắng học.",
        suggested_call_script=[
            "Chào phụ huynh, em là CSKH trung tâm CMS EDU.",
            "Em gọi thông báo tình hình học tập rất tốt của bé Minh.",
            "Tuy nhiên đợt này em thấy bé có vắng 2 buổi gần nhất, không biết gia đình có việc bận gì không ạ?",
            "Trung tâm có lớp học bù vào thứ 7 để bé theo kịp bài.",
            "Em xin phép hỗ trợ phụ huynh đặt lịch học bù cho bé ngay hôm nay nhé ạ.",
        ],
    )


def test_compute_context_fingerprint_deterministic_and_sensitive():
    """Dấu vân tay phải đồng nhất với cùng dữ liệu, nhưng đổi nợ hoặc vắng là đổi ngay."""
    ctx1 = _create_sample_context(debt=150000.0, absent_count=2, score=8.5)
    ctx2 = _create_sample_context(debt=150000.0, absent_count=2, score=8.5)
    # Khác retrieved_at nhưng cùng facts -> fingerprint phải giống nhau!
    ctx2.retrieved_at = datetime(2026, 11, 4, 12, 0, 0)
    assert compute_context_fingerprint(ctx1) == compute_context_fingerprint(ctx2)

    # Đổi nợ học phí -> Fingerprint thay đổi bắt buộc
    ctx_debt_changed = _create_sample_context(debt=500000.0, absent_count=2, score=8.5)
    assert compute_context_fingerprint(ctx1) != compute_context_fingerprint(ctx_debt_changed)

    # Đổi số buổi vắng -> Fingerprint thay đổi bắt buộc
    ctx_absent_changed = _create_sample_context(debt=150000.0, absent_count=3, score=8.5)
    assert compute_context_fingerprint(ctx1) != compute_context_fingerprint(ctx_absent_changed)

    # Đổi điểm số Module 1 -> Fingerprint thay đổi bắt buộc
    ctx_score_changed = _create_sample_context(debt=150000.0, absent_count=2, score=6.5)
    assert compute_context_fingerprint(ctx1) != compute_context_fingerprint(ctx_score_changed)


def test_question_normalization_and_cache_key():
    """Chuẩn hóa câu hỏi loại bỏ khoảng trắng và case, nhưng khác câu hỏi là khác khóa."""
    ctx = _create_sample_context()
    q1 = "Tình hình học viên thế nào?"
    q2 = "   tình hình   học viên   thế nào?  "
    q3 = "Học sinh còn nợ bao nhiêu tiền?"

    assert normalize_question(q1) == normalize_question(q2)

    key1 = generate_cache_key("scope_cskh", 105, ctx, q1)
    key2 = generate_cache_key("scope_cskh", 105, ctx, q2)
    key3 = generate_cache_key("scope_cskh", 105, ctx, q3)

    assert key1 == key2, "Hai câu hỏi giống nhau về nghĩa chữ phải sinh cùng cache key"
    assert key1 != key3, "Câu hỏi khác nhau phải sinh cache key khác nhau"

    # Khác scope phân quyền phải sinh key khác nhau (Security Isolation)
    key_diff_scope = generate_cache_key("scope_admin", 105, ctx, q1)
    assert key1 != key_diff_scope


def test_in_memory_cache_hit_and_tokens_saved():
    """Kiểm tra lưu, trúng cache và bộ đếm token tiết kiệm."""
    cache = ChatbotResponseCache(max_entries=10, ttl_seconds=60)
    res = _create_sample_response()
    key = "test_key_001"

    # Ban đầu miss
    assert cache.get(key) is None

    # Lưu vào cache với prompt ước lượng 1500 tokens
    cache.set(key, res, prompt_tokens=1500)

    # Tra cứu trúng
    cached = cache.get(key)
    assert cached is not None
    assert cached.summary == res.summary
    cache.record_hit(key)

    metrics = cache.get_metrics()
    assert metrics["hits"] == 1
    assert metrics["estimated_tokens_saved"] > 1500


def test_cache_ttl_expiration():
    """Kiểm tra hết hạn TTL tự động thu hồi."""
    cache = ChatbotResponseCache(max_entries=10, ttl_seconds=1)
    res = _create_sample_response()
    key = "expiring_key"

    cache.set(key, res)
    assert cache.get(key) is not None

    # Giả lập thời gian trôi qua bằng cách ghi đè timestamp
    cache._cache[key]["expires_at"] = 0.0
    assert cache.get(key) is None, "Phải hết hạn khi quá TTL"


def test_cache_lru_eviction():
    """Kiểm tra giải thuật LRU đẩy phần tử cũ nhất ra khi đầy bộ nhớ."""
    cache = ChatbotResponseCache(max_entries=2, ttl_seconds=60)
    res = _create_sample_response()

    cache.set("k1", res)
    cache.set("k2", res)
    assert len(cache._cache) == 2

    # Thêm phần tử thứ 3 -> k1 bị loại bỏ
    cache.set("k3", res)
    assert len(cache._cache) == 2
    assert cache.get("k1") is None
    assert cache.get("k2") is not None
    assert cache.get("k3") is not None


@pytest.mark.anyio
async def test_single_flight_stampede_prevention():
    """Kiểm tra Single-Flight Lock: 5 cuộc gọi đồng thời cho cùng 1 key chỉ gọi LLM 1 lần."""
    cache = ChatbotResponseCache(max_entries=10, ttl_seconds=60)
    res = _create_sample_response()
    call_count = 0

    async def _mock_compute():
        nonlocal call_count
        call_count += 1
        await asyncio.sleep(0.05)  # Giả lập độ trễ mạng LLM
        return res

    # Gọi 5 tasks song song cùng 1 key
    tasks = [cache.get_or_compute("same_key", _mock_compute, prompt_text="prompt") for _ in range(5)]
    results = await asyncio.gather(*tasks)

    # Tất cả 5 tasks đều nhận được kết quả hợp lệ
    for r, _ in results:
        assert r.summary == res.summary

    # Nhưng compute chỉ được kích hoạt ĐÚNG 1 LẦN duy nhất!
    assert call_count == 1, f"Compute phải được gọi đúng 1 lần, thực tế={call_count}"


def test_context_compaction_reduces_token_length():
    """Kiểm tra Tầng 2: Nén ngữ cảnh giảm kích thước ký tự/token đáng kể so với raw dump."""
    ctx = _create_sample_context()
    raw_dict = ctx.model_dump(mode="json")
    compact_dict = compact_student_context(ctx)

    import json
    raw_str = json.dumps(raw_dict, ensure_ascii=False)
    compact_str = json.dumps(compact_dict, ensure_ascii=False)

    raw_tokens = estimate_tokens(raw_str)
    compact_tokens = estimate_tokens(compact_str)

    assert compact_tokens < raw_tokens
    # Tỷ lệ tiết kiệm ít nhất 20%
    savings_pct = (raw_tokens - compact_tokens) / raw_tokens
    assert savings_pct >= 0.20, f"Mức tiết kiệm {savings_pct*100:.1f}% phải >= 20%"

