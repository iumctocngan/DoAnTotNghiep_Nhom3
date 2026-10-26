import asyncio
import logging
from dataclasses import replace
from functools import lru_cache
from threading import Lock

from retrieval.queries import SearchHit

logger = logging.getLogger(__name__)
_LOCK = Lock()

RERANKER_MODEL = "cross-encoder/mmarco-mMiniLMv2-L12-H384-v1"

@lru_cache(maxsize=1)
def _get_model(model_name: str):
    from sentence_transformers import CrossEncoder

    try:
        return CrossEncoder(model_name, device="cpu", local_files_only=True)
    except Exception:
        return CrossEncoder(model_name, device="cpu")


def _passage(hit: SearchHit) -> str:
    parts = [hit.document_filename]
    if hit.chunk.page_number is not None:
        parts.append(f"Trang {hit.chunk.page_number}")
    if hit.chunk.section_title:
        parts.append(hit.chunk.section_title)
    return "\n".join([*parts, hit.chunk.text])


def _score_pairs(model_name: str, pairs: list[tuple[str, str]]) -> list[float]:
    model = _get_model(model_name)
    with _LOCK:
        scores = model.predict(pairs)
    return [float(score) for score in scores]


async def rerank_candidates(
    query: str,
    candidates: list[SearchHit],
    limit: int,
    model_name: str = RERANKER_MODEL,
) -> list[SearchHit]:
    if not candidates:
        return []
    try:
        scores = await asyncio.to_thread(
            _score_pairs,
            model_name,
            [(query, _passage(hit)) for hit in candidates],
        )
    except Exception:
        logger.warning("Reranker gặp sự cố; chuyển sang sử dụng thứ tự điểm RRF.", exc_info=True)
        return candidates[:limit]

    ranked = sorted(zip(scores, candidates, strict=True), key=lambda pair: pair[0], reverse=True)
    return [replace(hit, rerank_score=score) for score, hit in ranked[:limit]]
