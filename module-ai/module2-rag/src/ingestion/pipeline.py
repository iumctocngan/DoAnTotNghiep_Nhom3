import asyncio
from pathlib import Path
from uuid import UUID, uuid4
from zipfile import BadZipFile

import pymupdf
from docx.opc.exceptions import PackageNotFoundError
from qdrant_client import AsyncQdrantClient, models

from core import Settings, embed_documents, embed_sparse_documents
from ingestion.chunking import build_chunks, embedding_text
from ingestion.loaders import extract_document
from storage import delete_document_chunks, ensure_collection_exists, upsert_document_chunks

_EMBEDDING_BATCH_SIZE = 16


class DocumentContentError(ValueError):
    pass


async def ingest_document(
    notebook_id: UUID,
    document_id: UUID,
    document_filename: str,
    storage_path: Path,
    document_type: str,
    settings: Settings,
    client: AsyncQdrantClient,
) -> int:
    try:
        units = await asyncio.to_thread(extract_document, storage_path)
        drafts = await asyncio.to_thread(build_chunks, units)
    except (ValueError, BadZipFile, pymupdf.FileDataError, PackageNotFoundError) as error:
        raise DocumentContentError(
            "Không thể trích xuất văn bản từ tài liệu này. Vui lòng kiểm tra định dạng và nội dung file."
        ) from error
    if not drafts:
        raise DocumentContentError("Không tìm thấy văn bản hợp lệ trong tài liệu này.")

    points: list[models.PointStruct] = []
    for offset in range(0, len(drafts), _EMBEDDING_BATCH_SIZE):
        batch = drafts[offset : offset + _EMBEDDING_BATCH_SIZE]
        # Dense được ghép section_title vào để có ngữ cảnh. Sparse chỉ dùng thân chunk để tiêu đề không làm phình TF.
        dense_vectors, sparse_vectors = await asyncio.gather(
            embed_documents([embedding_text(d) for d in batch]),
            embed_sparse_documents([d.text for d in batch]),
        )
        for draft, dense, (indices, values) in zip(batch, dense_vectors, sparse_vectors, strict=True):
            location = draft.source.location
            points.append(
                models.PointStruct(
                    id=str(uuid4()),
                    vector={
                        "dense": dense,
                        "sparse": models.SparseVector(indices=indices, values=values),
                    },
                    payload={
                        "notebook_id": str(notebook_id),
                        "document_id": str(document_id),
                        "document_filename": document_filename,
                        "document_type": document_type,
                        "storage_path": str(storage_path.resolve()),
                        "chunk_index": draft.chunk_index,
                        "page_number": location.page_number,
                        "section_title": location.section_title,
                        "text": draft.text,
                    },
                )
            )

    await ensure_collection_exists(client, settings.qdrant_collection_name)
    await delete_document_chunks(client, settings.qdrant_collection_name, document_id)
    await upsert_document_chunks(client, settings.qdrant_collection_name, points)
    return len(points)
