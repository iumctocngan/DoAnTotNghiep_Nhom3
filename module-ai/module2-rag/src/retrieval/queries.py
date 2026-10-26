from dataclasses import dataclass
from uuid import UUID

from qdrant_client import AsyncQdrantClient, models

from storage import DocumentChunk

BRANCH_LIMIT = 30
RRF_LIMIT = 15


@dataclass(frozen=True)
class SearchHit:
    chunk: DocumentChunk
    document_filename: str
    document_type: str
    fusion_score: float = 0.0
    rerank_score: float | None = None

    @property
    def citation_score(self) -> float | None:
        # Cross-encoder trả về logit (~ -10 đến +10). Nếu không rerank thì trả về điểm RRF (~ 0.01 đến 0.03).
        if self.rerank_score is not None:
            return self.rerank_score
        return self.fusion_score or None


def _match(key: str, value: object) -> models.FieldCondition:
    return models.FieldCondition(key=key, match=models.MatchValue(value=value))


def point_to_search_hit(point: models.ScoredPoint | models.Record) -> SearchHit:
    payload = point.payload or {}
    return SearchHit(
        chunk=DocumentChunk(
            id=UUID(str(point.id)),
            document_id=UUID(payload["document_id"]),
            notebook_id=UUID(payload["notebook_id"]),
            chunk_index=payload.get("chunk_index", 0),
            text=payload.get("text", ""),
            page_number=payload.get("page_number"),
            section_title=payload.get("section_title"),
            storage_path=payload.get("storage_path"),
        ),
        document_filename=payload.get("document_filename", ""),
        document_type=payload.get("document_type", "pdf"),
        fusion_score=float(getattr(point, "score", 0.0) or 0.0),
    )


async def hybrid_search(
    client: AsyncQdrantClient,
    collection_name: str,
    notebook_id: UUID,
    dense_vector: list[float],
    sparse_indices: list[int],
    sparse_values: list[float],
    document_ids: list[UUID] | None = None,
) -> list[SearchHit]:
    notebook_filter = models.Filter(must=[_match("notebook_id", str(notebook_id))])
    must_conditions = [_match("notebook_id", str(notebook_id))]
    if document_ids:
        must_conditions.append(
            models.FieldCondition(
                key="document_id",
                match=models.MatchAny(any=[str(d) for d in document_ids]),
            )
        )
    scope = models.Filter(must=must_conditions)

    response = await client.query_points(
        collection_name=collection_name,
        prefetch=[
            models.Prefetch(
                query=dense_vector,
                using="dense",
                filter=scope,
                limit=BRANCH_LIMIT,
                params=models.SearchParams(hnsw_ef=64),
            ),
            models.Prefetch(
                query=models.SparseVector(indices=sparse_indices, values=sparse_values),
                using="sparse",
                filter=scope,
                limit=BRANCH_LIMIT,
                # IDF tính trên toàn notebook (không chỉ document đang lọc) để điểm BM25 ổn định khi người dùng chọn ít tài liệu.
                params=models.SearchParams(idf=models.IdfCorpusParams(corpus=notebook_filter)),
            ),
        ],
        query=models.RrfQuery(rrf=models.Rrf(k=60)),
        limit=RRF_LIMIT,
    )
    return [point_to_search_hit(p) for p in response.points]


async def document_scope_chunks(
    client: AsyncQdrantClient,
    collection_name: str,
    notebook_id: UUID,
    document_id: UUID,
    start_page: int | None = None,
    end_page: int | None = None,
) -> list[SearchHit]:
    must_conditions: list[models.Condition] = [
        _match("notebook_id", str(notebook_id)),
        _match("document_id", str(document_id)),
    ]
    if start_page is not None or end_page is not None:
        range_kwargs: dict[str, float] = {}
        if start_page is not None:
            range_kwargs["gte"] = float(start_page)
        if end_page is not None:
            range_kwargs["lte"] = float(end_page)
        must_conditions.append(
            models.FieldCondition(key="page_number", range=models.Range(**range_kwargs))
        )

    scope_filter = models.Filter(must=must_conditions)
    records: list[models.Record] = []
    offset = None
    while True:
        batch, next_offset = await client.scroll(
            collection_name=collection_name,
            scroll_filter=scope_filter,
            limit=100,
            offset=offset,
            with_payload=True,
        )
        records.extend(batch)
        if next_offset is None:
            break
        offset = next_offset

    return sorted(
        (point_to_search_hit(r) for r in records),
        key=lambda hit: (hit.chunk.page_number or 0, hit.chunk.chunk_index),
    )

