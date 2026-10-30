from uuid import UUID

from qdrant_client import AsyncQdrantClient

from core import Settings, embed_hybrid_query
from retrieval.queries import SearchHit, hybrid_search
from retrieval.reranker import rerank_candidates


async def search_knowledge_context(
    query: str,
    top_k: int,
    settings: Settings,
    client: AsyncQdrantClient,
    document_ids: list[UUID] | None = None,
) -> list[SearchHit]:
    dense_vector, sparse_indices, sparse_values = await embed_hybrid_query(query)
    candidates = await hybrid_search(
        client,
        settings.qdrant_collection_name,
        dense_vector,
        sparse_indices,
        sparse_values,
        document_ids=document_ids,
    )
    return await rerank_candidates(query, candidates, top_k)
