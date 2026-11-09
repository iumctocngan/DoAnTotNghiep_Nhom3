"""Router Context Retriever và sinh kịch bản CSKH cho Tuần 3.

FastAPI chỉ đọc snapshot nghiệp vụ. Core API đảm nhận JWT/RBAC, lưu session,
tin nhắn và audit để không mở quyền ghi trực tiếp cho dịch vụ AI.
Tích hợp bộ đệm phản hồi xác định (Deterministic Cache) nhằm tối ưu hóa token và độ trễ.
"""
from fastapi import APIRouter, Depends, Header, HTTPException, status
from sqlalchemy.orm import Session

from ai_service.config import settings
from ai_service.database import get_db
from ai_service.schemas.cskh_schemas import CallScriptResponse, GenerateCallScriptRequest, StudentContext
from ai_service.services.cskh_cache import chatbot_cache, generate_cache_key
from ai_service.services.cskh_prompt import OUT_OF_DOMAIN_MESSAGE, build_prompt, is_cskh_question
from ai_service.services.context_retriever import retrieve_student_context
from ai_service.services.llm_client import generate_call_script

router = APIRouter()


@router.get(
    "/cskh/student-context/{student_id}",
    response_model=StudentContext,
    summary="Lấy context đầy đủ của học sinh để inject vào LLM Chatbot",
    description=(
        "Gom dữ liệu từ 3 nguồn: Core Platform (chuyên cần, học phí), "
        "Module 1 (điểm, nhận xét GV), Module 3 (dự đoán tái phí). "
        "Dữ liệu trả về là fact-grounded — không tự suy diễn. "
        "Không trả dữ liệu thay thế khi nguồn dữ liệu thiếu."
    ),
)
async def get_student_context(
    student_id: int,
    db: Session = Depends(get_db),
):
    try:
        return retrieve_student_context(db, student_id)
    except ValueError as exc:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(exc)) from exc


@router.post(
    "/cskh/call-script",
    response_model=CallScriptResponse,
    summary="Sinh kịch bản gọi điện từ context học viên đã được grounded (kèm tối ưu Token Cache)",
)
async def create_call_script(
    request: GenerateCallScriptRequest,
    db: Session = Depends(get_db),
    x_authorization_scope: str = Header(default="default_scope", alias="X-Authorization-Scope"),
):
    """Không lưu DB: Core API chịu trách nhiệm xác thực, RBAC và lưu lịch sử chat.
    Áp dụng bộ đệm phản hồi xác định: Khi các biến số đồng nhất, trả kết quả tức thời
    và tiêu thụ 0 token LLM.
    """
    if not is_cskh_question(request.question):
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=OUT_OF_DOMAIN_MESSAGE)
    try:
        context = retrieve_student_context(db, request.student_id)
    except ValueError as exc:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(exc)) from exc

    # Áp dụng Tầng 2 Context Compaction để giảm 20% - 35% tokens
    prompt = build_prompt(context, request.question, compact=True)

    def _call_llm() -> CallScriptResponse:
        try:
            return generate_call_script(prompt)
        except RuntimeError as exc:
            # Không thay câu trả lời bằng dữ liệu suy đoán khi provider/configuration gặp lỗi.
            raise HTTPException(status_code=status.HTTP_503_SERVICE_UNAVAILABLE, detail=str(exc)) from exc

    if not settings.enable_chatbot_cache:
        return _call_llm()

    cache_key = generate_cache_key(
        scope=x_authorization_scope,
        student_id=request.student_id,
        context=context,
        question=request.question,
        model_name=settings.llm_model,
    )

    result, _ = await chatbot_cache.get_or_compute(cache_key, _call_llm, prompt_text=prompt)
    return result


@router.get(
    "/cskh/cache-metrics",
    summary="Lấy số liệu đo lường hiệu năng và lượng token tiết kiệm của bộ đệm Chatbot CSKH",
)
async def get_cache_metrics():
    """Báo cáo các chỉ số hit-rate, kích thước bộ đệm và tổng token ước lượng đã tiết kiệm."""
    return chatbot_cache.get_metrics()


@router.post(
    "/cskh/cache/clear",
    summary="Xóa toàn bộ bản ghi trong bộ đệm Chatbot CSKH (dành cho kiểm thử/quản trị)",
)
async def clear_cache():
    """Làm rỗng bộ đệm trong bộ nhớ và reset các bộ đếm."""
    chatbot_cache.clear()
    return {"message": "Đã xóa toàn bộ bộ đệm Chatbot CSKH thành công"}
