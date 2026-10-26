import logging
from uuid import UUID

from pydantic import ValidationError
from qdrant_client import AsyncQdrantClient

from core import Settings
from generation.context import build_context
from generation.gemini import generate
from generation.prompts import build_clo_rubric_prompt
from generation.schemas import (
    GeneratedCloRubricPayload,
    GeneratedLearningOutcome,
    GeneratedRubricCriterion,
    GeneratedRubricLevel,
)
from retrieval import document_scope_chunks

logger = logging.getLogger(__name__)

RUBRIC_LEVELS = (
    ("1: Chưa đạt", 1.0, 1.0),
    ("2: Cần cải thiện", 2.0, 2.0),
    ("3: Đạt", 3.0, 3.0),
    ("4: Tốt", 4.0, 4.0),
    ("5: Rất tốt", 5.0, 5.0),
)


class CloRubricGenerationError(RuntimeError):
    pass


async def generate_clo_rubric(
    notebook_id: UUID,
    document_id: UUID,
    settings: Settings,
    client: AsyncQdrantClient,
    outcome_count: int = 3,
    start_page: int | None = None,
    end_page: int | None = None,
) -> list[GeneratedLearningOutcome]:
    hits = await document_scope_chunks(
        client,
        settings.qdrant_collection_name,
        notebook_id,
        document_id,
        start_page=start_page,
        end_page=end_page,
    )
    if not hits:
        scope = (
            f"từ trang {start_page} đến {end_page}"
            if (start_page is not None or end_page is not None)
            else "trong tài liệu"
        )
        raise CloRubricGenerationError(f"Không tìm thấy nội dung {scope} để tạo chuẩn đầu ra.")

    context = build_context(hits)
    chunk_ids = [hit.chunk.id for hit in hits]
    prompt = build_clo_rubric_prompt(context, outcome_count)

    try:
        raw_json = await generate(
            prompt,
            api_key=settings.gemini_api_key,
            response_schema=GeneratedCloRubricPayload,
            request_timeout_ms=60_000,
        )
        payload = GeneratedCloRubricPayload.model_validate_json(raw_json)
    except (ValidationError, Exception) as error:
        logger.warning("Lỗi sinh CLO/Rubric từ Gemini: %s", error)
        raise CloRubricGenerationError(f"Mô hình trả về dữ liệu không hợp lệ: {error}") from error

    outcomes: list[GeneratedLearningOutcome] = []
    for item in payload.outcomes[:outcome_count]:
        source_chunk_ids = [
            chunk_ids[i - 1] for i in dict.fromkeys(item.source_ids) if 1 <= i <= len(chunk_ids)
        ] or [chunk_ids[0]]

        criteria = [
            GeneratedRubricCriterion(
                title=c.title,
                description=c.description,
                levels=[
                    GeneratedRubricLevel(
                        name=name,
                        minimum_score=min_s,
                        maximum_score=max_s,
                        description=lvl.description,
                    )
                    for lvl, (name, min_s, max_s) in zip(c.levels, RUBRIC_LEVELS, strict=False)
                ],
            )
            for c in item.criteria
        ]
        outcomes.append(
            GeneratedLearningOutcome(
                code=item.code,
                description=item.description,
                source_chunk_ids=source_chunk_ids,
                criteria=criteria,
            )
        )

    if not outcomes:
        raise CloRubricGenerationError("Không tạo được chuẩn đầu ra nào từ nội dung tài liệu.")

    return outcomes
