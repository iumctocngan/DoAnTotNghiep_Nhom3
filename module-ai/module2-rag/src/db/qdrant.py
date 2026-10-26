from dataclasses import dataclass
from functools import lru_cache
import logging
from uuid import UUID

from qdrant_client import AsyncQdrantClient, models

from core import EMBEDDING_DIMENSION

logger = logging.getLogger(__name__)


@dataclass(frozen=True)
class DocumentChunk:
    id: UUID
    document_id: UUID
    notebook_id: UUID
    chunk_index: int
    text: str
    page_number: int | None = None
    section_title: str | None = None
    storage_path: str | None = None


@lru_cache(maxsize=1)
def get_qdrant_client(url: str) -> AsyncQdrantClient:
    return AsyncQdrantClient(url=url)


def _field_filter(key: str, value: UUID) -> models.FilterSelector:
    return models.FilterSelector(
        filter=models.Filter(
            must=[models.FieldCondition(key=key, match=models.MatchValue(value=str(value)))]
        )
    )


async def ensure_collection_exists(
    client: AsyncQdrantClient,
    collection_name: str,
) -> None:
    if await client.collection_exists(collection_name):
        return

    logger.info("Đang khởi tạo Qdrant collection: %s", collection_name)
    await client.create_collection(
        collection_name=collection_name,
        vectors_config={
            "dense": models.VectorParams(size=EMBEDDING_DIMENSION, distance=models.Distance.COSINE),
        },
        sparse_vectors_config={
            "sparse": models.SparseVectorParams(modifier=models.Modifier.IDF),
        },
    )
    for field_name, schema in (
        ("notebook_id", models.UuidIndexParams(type=models.UuidIndexType.UUID, is_tenant=True)),
        ("document_id", models.PayloadSchemaType.UUID),
        ("page_number", models.PayloadSchemaType.INTEGER),
    ):
        await client.create_payload_index(
            collection_name=collection_name,
            field_name=field_name,
            field_schema=schema,
        )
    logger.info("Đã tạo thành công Qdrant collection: %s", collection_name)


async def delete_document_chunks(
    client: AsyncQdrantClient,
    collection_name: str,
    document_id: UUID,
) -> None:
    if not await client.collection_exists(collection_name):
        return
    await client.delete(
        collection_name=collection_name,
        points_selector=_field_filter("document_id", document_id),
    )


async def delete_notebook_chunks(
    client: AsyncQdrantClient,
    collection_name: str,
    notebook_id: UUID,
) -> None:
    if not await client.collection_exists(collection_name):
        return
    await client.delete(
        collection_name=collection_name,
        points_selector=_field_filter("notebook_id", notebook_id),
    )


async def upsert_document_chunks(
    client: AsyncQdrantClient,
    collection_name: str,
    points: list[models.PointStruct],
) -> None:
    if points:
        await client.upsert(collection_name=collection_name, points=points)
