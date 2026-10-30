import logging
from datetime import datetime, timezone, timedelta
from uuid import UUID, uuid4

from qdrant_client import AsyncQdrantClient, models

from core import Settings
from core.embeddings import embed_hybrid_query
from retrieval.queries import Citation

logger = logging.getLogger(__name__)


async def lookup_semantic_cache(
    question: str,
    settings: Settings,
    client: AsyncQdrantClient,
    document_ids: list[UUID] | None = None,
) -> tuple[str, list[Citation]] | None:
    """
    Looks up semantically similar question in Qdrant cache collection.
    Follows official Qdrant Semantic Cache tutorial:
    - Cosine similarity threshold >= settings.cache_similarity_threshold (e.g. 0.95)
    - Validates expiry datetime (expires_at > now)
    - Validates that source_doc_ids in the cached answer are allowed within document_ids
    """
    try:
        if not await client.collection_exists(settings.qdrant_cache_collection_name):
            return None
    except Exception:
        return None

    try:
        dense_vector, _, _ = await embed_hybrid_query(question)
    except Exception as err:
        logger.warning("Không thể embed câu hỏi cho Semantic Cache: %s", err)
        return None

    now_iso = datetime.now(timezone.utc).isoformat()
    query_filter = models.Filter(
        must=[
            models.FieldCondition(
                key="expires_at",
                range=models.DatetimeRange(gt=now_iso),
            )
        ]
    )

    try:
        response = await client.query_points(
            collection_name=settings.qdrant_cache_collection_name,
            query=dense_vector,
            query_filter=query_filter,
            limit=1,
            score_threshold=settings.cache_similarity_threshold,
        )
    except Exception as err:
        logger.warning("Lỗi tra cứu Semantic Cache Qdrant: %s", err)
        return None

    if not response.points:
        return None

    best_match = response.points[0]
    payload = best_match.payload or {}

    cached_source_ids = payload.get("source_doc_ids") or []
    if document_ids is not None:
        allowed_str_set = {str(d) for d in document_ids}
        if any(src not in allowed_str_set for src in cached_source_ids):
            logger.info("Semantic cache match rejected: source doc not in allowed document_ids.")
            return None

    logger.info(
        "⚡ Qdrant Semantic Cache HIT (Score: %.4f) | Q: '%s' ≈ Cached: '%s'",
        best_match.score or 0.0,
        question,
        payload.get("question", ""),
    )

    answer = payload.get("answer", "")
    citations_data = payload.get("citations", [])
    citations: list[Citation] = []
    for c in citations_data:
        try:
            citations.append(
                Citation(
                    chunk_id=UUID(c["chunk_id"]),
                    document_id=UUID(c["document_id"]),
                    document_name=c.get("document_name", ""),
                    label=c.get("label", ""),
                    page_number=c.get("page_number"),
                    section_title=c.get("section_title"),
                    score=c.get("score"),
                )
            )
        except Exception:
            continue

    return answer, citations


async def upsert_semantic_cache(
    question: str,
    answer: str,
    citations: list[Citation],
    settings: Settings,
    client: AsyncQdrantClient,
) -> None:
    """
    Saves an answer into Qdrant Semantic Cache with its vector, metadata, source_doc_ids, and TTL.
    """
    try:
        if not await client.collection_exists(settings.qdrant_cache_collection_name):
            return
    except Exception:
        return

    try:
        dense_vector, _, _ = await embed_hybrid_query(question)
    except Exception as err:
        logger.warning("Không thể embed câu hỏi khi lưu Semantic Cache: %s", err)
        return

    source_doc_ids = list({str(c.document_id) for c in citations})
    expires_at = (datetime.now(timezone.utc) + timedelta(days=settings.cache_ttl_days)).isoformat()

    citations_payload = [
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
    ]

    try:
        await client.upsert(
            collection_name=settings.qdrant_cache_collection_name,
            points=[
                models.PointStruct(
                    id=str(uuid4()),
                    vector=dense_vector,
                    payload={
                        "question": question,
                        "answer": answer,
                        "source_doc_ids": source_doc_ids,
                        "citations": citations_payload,
                        "expires_at": expires_at,
                    },
                )
            ],
        )
        logger.info(
            "Đã lưu câu hỏi vào Qdrant Semantic Cache: '%s' (TTL: %d ngày)",
            question,
            settings.cache_ttl_days,
        )
    except Exception as err:
        logger.warning("Không thể lưu Semantic Cache vào Qdrant: %s", err)
