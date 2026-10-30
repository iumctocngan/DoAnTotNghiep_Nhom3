from dataclasses import dataclass
from uuid import UUID

from pydantic import ValidationError
from qdrant_client import AsyncQdrantClient

from core import Settings
from generation.context import Citation, build_context, make_citation
from generation.gemini import GeminiGenerationError, generate
from generation.prompts import build_chat_prompt
from generation.schemas import GeneratedAnswerDraft
from retrieval import search_knowledge_context

_CHAT_RETRIEVAL_TOP_K = 5

NO_EVIDENCE_ANSWER = "Tôi chưa tìm thấy thông tin phù hợp trong tài liệu học liệu được phân phối."
OUT_OF_SCOPE_ANSWER = (
    "Yêu cầu này nằm ngoài phạm vi tra cứu tài liệu của trợ lý RAG. "
    "Tôi có thể giúp tra cứu, tóm tắt hoặc giải thích nội dung có trong tài liệu."
)

@dataclass(frozen=True)
class GroundedAnswer:
    answer: str
    citations: list[Citation]


async def answer_question(
    question: str,
    settings: Settings,
    client: AsyncQdrantClient,
    document_ids: list[UUID] | None = None,
    history: list[dict] | None = None,
) -> GroundedAnswer:
    hits = await search_knowledge_context(
        question,
        _CHAT_RETRIEVAL_TOP_K,
        settings,
        client,
        document_ids,
    )
    if not hits:
        return GroundedAnswer(NO_EVIDENCE_ANSWER, [])

    prompt = build_chat_prompt(question, build_context(hits), history)
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
        return GroundedAnswer(OUT_OF_SCOPE_ANSWER, [])

    answer = draft.answer.strip()
    if draft.status != "answered" or not answer:
        return GroundedAnswer(NO_EVIDENCE_ANSWER, [])

    source_ids = [i for i in dict.fromkeys(draft.used_source_ids) if 1 <= i <= len(hits)]
    citations = [make_citation(hits[idx - 1]) for idx in source_ids]

    return GroundedAnswer(answer=answer, citations=citations)
