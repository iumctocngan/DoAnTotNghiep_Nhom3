import asyncio
import logging
import time
from datetime import datetime, timedelta, timezone
from uuid import UUID, uuid4

from qdrant_client import AsyncQdrantClient, models

from core import Settings
from core.embeddings import embed_dense_query
from db import cleanup_expired_semantic_cache
from retrieval.queries import Citation

logger = logging.getLogger(__name__)
_last_cleanup = 0.0


def _dict_to_citation(c: dict) -> Citation:
    return Citation(
        chunk_id=UUID(c["chunk_id"]),
        document_id=UUID(c["document_id"]),
        document_name=c.get("document_name", ""),
        label=c.get("label", ""),
        page_number=c.get("page_number"),
        section_title=c.get("section_title"),
        score=c.get("score"),
    )


async def lookup_semantic_cache(
    question: str,
    settings: Settings,
    client: AsyncQdrantClient,
    document_ids: list[UUID] | None = None,
) -> tuple[str, list[Citation]] | None:
    """Tra cứu câu hỏi tương đồng ngữ nghĩa trong Qdrant Cache collection."""
    try:
        dense_vector = await embed_dense_query(question)
        now_iso = datetime.now(timezone.utc).isoformat()

        response = await client.query_points(
            collection_name=settings.qdrant_cache_collection_name,
            query=dense_vector,
            query_filter=models.Filter(
                must=[models.FieldCondition(key="expires_at", range=models.DatetimeRange(gt=now_iso))]
            ),
            limit=10,
            score_threshold=settings.cache_similarity_threshold,
        )
        if not response.points:
            return None

        allowed = {str(d) for d in document_ids} if document_ids is not None else None
        for point in response.points:
            p = point.payload or {}
            cached_sources = p.get("source_doc_ids", [])
            if allowed is not None and any(src not in allowed for src in cached_sources):
                continue

            logger.info(
                " Khớp Semantic Cache Qdrant (Độ tương đồng: %.4f) | Câu hỏi: '%s' ≈ Câu hỏi trong Cache: '%s'",
                point.score or 0.0,
                question,
                p.get("question", ""),
            )
            citations = [_dict_to_citation(c) for c in p.get("citations", [])]
            return p.get("answer", ""), citations

        logger.info("Bỏ qua kết quả Semantic Cache: Tài liệu nguồn không nằm trong danh sách document_ids được phép truy cập.")
        return None
    except Exception as err:
        logger.warning("Lỗi tra cứu Semantic Cache Qdrant: %s", err)
        return None


async def upsert_semantic_cache(
    question: str,
    answer: str,
    citations: list[Citation],
    settings: Settings,
    client: AsyncQdrantClient,
) -> None:
    """Lưu câu trả lời vào Qdrant Semantic Cache với vector, payload, nguồn và TTL."""
    global _last_cleanup
    now_ts = time.time()
    if now_ts - _last_cleanup > 3600.0:
        _last_cleanup = now_ts
        asyncio.create_task(cleanup_expired_semantic_cache(client, settings.qdrant_cache_collection_name))

    try:
        dense_vector = await embed_dense_query(question)
        expires_at = (datetime.now(timezone.utc) + timedelta(days=settings.cache_ttl_days)).isoformat()
        payload = {
            "question": question,
            "answer": answer,
            "source_doc_ids": list({str(c.document_id) for c in citations}),
            "citations": [
                {
                    "chunk_id": str(c.chunk_id),
                    "document_id": str(c.document_id),
                    "document_name": c.document_name,
                    "label": c.label,
                    "page_number": c.page_number,
                    "section_title": c.section_title,
                    "score": c.score,
                }
                for c in citations
            ],
            "expires_at": expires_at,
        }
        await client.upsert(
            collection_name=settings.qdrant_cache_collection_name,
            points=[models.PointStruct(id=str(uuid4()), vector=dense_vector, payload=payload)],
        )
        logger.info(
            "Đã lưu câu hỏi vào Qdrant Semantic Cache: '%s' (TTL: %d ngày)",
            question,
            settings.cache_ttl_days,
        )
    except Exception as err:
        logger.warning("Không thể lưu Semantic Cache vào Qdrant: %s", err)
