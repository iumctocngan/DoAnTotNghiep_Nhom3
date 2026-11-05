import asyncio
import logging
from dataclasses import dataclass
from uuid import UUID

from pydantic import ValidationError
from qdrant_client import AsyncQdrantClient

from core import Settings
from generation.context import Citation, build_context, make_citation
from generation.gemini import GeminiGenerationError, generate
from generation.memory import process_conversation_memory
from generation.prompts import build_chat_prompt
from generation.schemas import GeneratedAnswerDraft
from retrieval import (
    lookup_semantic_cache,
    search_knowledge_context,
    upsert_semantic_cache,
)

logger = logging.getLogger(__name__)

_TOP_K = 5
NO_EVIDENCE_ANSWER = "Tôi chưa tìm thấy thông tin phù hợp trong tài liệu học liệu được phân phối."
OUT_OF_SCOPE_ANSWER = (
    "Yêu cầu nằm ngoài phạm vi hỗ trợ. Tôi chỉ trả lời câu hỏi dựa trên tài liệu học liệu có sẵn."
)


@dataclass(frozen=True)
class GroundedAnswer:
    answer: str
    citations: list[Citation]
    new_summary: str | None = None
    new_summary_processed_count: int = 0


async def rewrite_standalone_question(
    question: str,
    history: list[dict] | None,
    api_key: str | list[str],
) -> str:
    """
    Chuẩn hóa câu hỏi theo mẫu LangChain History-Aware:
    - Nếu không có lịch sử: Giữ nguyên 100%, không gọi LLM (0ms).
    - Nếu có lịch sử: LLM tự nhận diện ngữ cảnh, chỉ viết lại khi câu hỏi có đại từ tham chiếu
      hoặc thiếu ngữ cảnh; nếu câu hỏi đã độc lập thì giữ nguyên nguyên văn.
    """
    if not history:
        return question.strip()

    recent_turns = "\n".join(
        f"{'User' if m.get('role') == 'user' else 'AI'}: {str(m.get('content', '')).strip()}"
        for m in history[-4:]
    )
    prompt = (
        "Dựa vào lịch sử hội thoại gần nhất và câu hỏi mới của người dùng:\n"
        "- Nếu câu hỏi là câu cụt, chứa đại từ mơ hồ ('nó', 'cái đó', 'bước trên') hoặc liên từ nối ('còn...', 'thế thì...'), "
        "hãy bổ sung ngữ cảnh từ lịch sử để tạo thành MỘT CÂU HỎI ĐỘC LẬP có nghĩa hoàn chỉnh.\n"
        "- Nếu câu hỏi ĐÃ ĐỘC LẬP hoặc chuyển sang chủ đề khác, HÃY GIỮ NGUYÊN câu hỏi gốc.\n"
        "TUYỆT ĐỐI chỉ trả về duy nhất câu hỏi, không thêm giải thích hay dấu ngoặc kép.\n\n"
        "Ví dụ mẫu trực quan:\n"
        "- Lịch sử: 'Định lý Pytago là gì?' -> Câu hỏi: 'Tại sao nó lại bằng c^2?' -> Viết lại: 'Tại sao trong định lý Pytago a^2 + b^2 lại bằng c^2?'\n"
        "- Lịch sử: 'Đặc điểm hình tròn' -> Câu hỏi: 'Còn chu vi?' -> Viết lại: 'Công thức tính chu vi hình tròn là gì?'\n"
        "- Lịch sử: 'Định nghĩa hình thang cân' -> Câu hỏi: 'Chứng minh thế nào?' -> Viết lại: 'Làm thế nào để chứng minh một hình là hình thang cân?'\n"
        "- Lịch sử: 'Bài tập hàm số' -> Câu hỏi: 'Lịch thi giữa kỳ khi nào bắt đầu?' -> Viết lại: 'Lịch thi giữa kỳ khi nào bắt đầu?' (Giữ nguyên)\n\n"
        f"Lịch sử hội thoại:\n{recent_turns}\n\n"
        f"Câu hỏi: {question}\n\n"
        "Câu hỏi độc lập:"
    )

    try:
        rewritten = await generate(prompt, api_key=api_key, request_timeout_ms=10_000)
        cleaned = rewritten.strip().strip('"').strip("'")
        if cleaned:
            if cleaned != question.strip():
                logger.info("Viết lại câu hỏi độc lập: '%s' -> '%s'", question, cleaned)
            return cleaned
    except Exception as err:
        logger.warning("Lỗi rewrite câu hỏi: %s. Dùng câu hỏi gốc.", err)

    return question.strip()


async def answer_question(
    question: str,
    settings: Settings,
    client: AsyncQdrantClient,
    document_ids: list[UUID] | None = None,
    history: list[dict] | None = None,
    conversation_id: UUID | None = None,
    summary: str | None = None,
    summary_processed_count: int = 0,
) -> GroundedAnswer:
    """
    Quy trình trả lời câu hỏi chuẩn RAG:
    1. Chuẩn hóa câu hỏi thành standalone nếu có ngữ cảnh hội thoại.
    2. Tra cứu Semantic Cache 1 lần theo search_question rõ nghĩa.
    3. Chạy song song Short-term Memory (SQL Persistent) và Hybrid Retrieval (Qdrant).
    4. Sinh câu trả lời với Gemini theo Grounded Context.
    5. Lưu kết quả thành công vào Semantic Cache.
    """
    cleaned_question = question.strip()

    # 1. Chuẩn hóa câu hỏi nếu có lịch sử (0ms nếu lượt đầu)
    search_question = await rewrite_standalone_question(
        question=cleaned_question,
        history=history,
        api_key=settings.gemini_api_key,
    )

    # 2. Tra cứu Semantic Cache
    cached = await lookup_semantic_cache(search_question, settings, client, document_ids)
    if cached is not None:
        cached_answer, cached_citations = cached
        return GroundedAnswer(
            answer=cached_answer,
            citations=cached_citations,
            new_summary=summary,
            new_summary_processed_count=summary_processed_count,
        )

    # 3. Chạy song song Short-term Memory và Hybrid Retrieval
    (curr_summary, recent_history, new_processed_count), hits = await asyncio.gather(
        process_conversation_memory(
            history=history,
            api_key=settings.gemini_api_key,
            conversation_id=conversation_id,
            current_summary=summary,
            current_processed_count=summary_processed_count,
            trigger_messages=10,
            keep_recent_count=4,
        ),
        search_knowledge_context(
            search_question,
            _TOP_K,
            settings,
            client,
            document_ids,
        ),
    )

    if not hits:
        return GroundedAnswer(
            answer=NO_EVIDENCE_ANSWER,
            citations=[],
            new_summary=curr_summary,
            new_summary_processed_count=new_processed_count,
        )

    # 4. Sinh câu trả lời với Gemini
    prompt = build_chat_prompt(
        question=search_question,
        context=build_context(hits),
        history=recent_history,
        summary=curr_summary,
    )
    raw_answer = await generate(
        prompt,
        api_key=settings.gemini_api_key,
        response_schema=GeneratedAnswerDraft,
    )

    try:
        draft = GeneratedAnswerDraft.model_validate_json(raw_answer)
    except ValidationError as error:
        raise GeminiGenerationError("Mô hình Gemini trả về câu trả lời không hợp lệ.") from error

    if draft.status == "out_of_scope":
        return GroundedAnswer(
            answer=OUT_OF_SCOPE_ANSWER,
            citations=[],
            new_summary=curr_summary,
            new_summary_processed_count=new_processed_count,
        )

    answer = draft.answer.strip()
    if draft.status != "answered" or not answer:
        return GroundedAnswer(
            answer=NO_EVIDENCE_ANSWER,
            citations=[],
            new_summary=curr_summary,
            new_summary_processed_count=new_processed_count,
        )

    source_ids = [i for i in dict.fromkeys(draft.used_source_ids) if 1 <= i <= len(hits)]
    citations = [make_citation(hits[idx - 1]) for idx in source_ids]

    # 5. Lưu kết quả thành công vào Semantic Cache
    if citations:
        await upsert_semantic_cache(search_question, answer, citations, settings, client)

    return GroundedAnswer(
        answer=answer,
        citations=citations,
        new_summary=curr_summary,
        new_summary_processed_count=new_processed_count,
    )
