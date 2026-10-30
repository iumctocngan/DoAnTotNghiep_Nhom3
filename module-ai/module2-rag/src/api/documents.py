from pathlib import Path
from uuid import UUID, uuid4

from fastapi import APIRouter, Depends, File, Form, HTTPException, Response, UploadFile, status
from fastapi.responses import FileResponse
from qdrant_client import AsyncQdrantClient

from api.dependencies import get_qdrant, get_request_settings
from api.schemas import DocumentOutlineItem, IngestionResponse
from api.security import CurrentUser, get_current_user
from core import Settings
from ingestion.pipeline import DocumentContentError, ingest_document
from db import (
    delete_document_chunks,
    find_any_document_file,
    get_pdf_outline,
    render_pdf_page_image,
    remove_uploaded_document,
    save_uploaded_document,
)

router = APIRouter(tags=["documents"])


# --- Centralized Knowledge Document Endpoints ---

@router.post(
    "/documents/ingest",
    response_model=IngestionResponse,
    status_code=status.HTTP_201_CREATED,
)
async def upload_and_ingest_knowledge_document(
    file: UploadFile = File(...),
    document_id: UUID | None = Form(default=None),
    user: CurrentUser = Depends(get_current_user),
    settings: Settings = Depends(get_request_settings),
    qdrant: AsyncQdrantClient = Depends(get_qdrant),
) -> IngestionResponse:
    if not file.filename:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Tên file không hợp lệ.",
        )

    extension = Path(file.filename).suffix.lstrip(".").lower()
    if extension not in {"pdf", "docx"}:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Hệ thống chỉ hỗ trợ định dạng PDF hoặc DOCX.",
        )

    doc_id = document_id or uuid4()
    saved_path = await save_uploaded_document(
        file=file,
        settings=settings,
        document_id=doc_id,
        document_type=extension,
    )

    try:
        chunk_count = await ingest_document(
            document_id=doc_id,
            document_filename=file.filename,
            storage_path=saved_path,
            document_type=extension,
            settings=settings,
            client=qdrant,
        )
    except DocumentContentError as error:
        remove_uploaded_document(saved_path)
        await delete_document_chunks(qdrant, settings.qdrant_collection_name, doc_id)
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=str(error),
        ) from error
    except Exception as error:
        remove_uploaded_document(saved_path)
        await delete_document_chunks(qdrant, settings.qdrant_collection_name, doc_id)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Quá trình lập chỉ mục tài liệu gặp lỗi hệ thống.",
        ) from error

    return IngestionResponse(document_id=doc_id, chunk_count=chunk_count)


@router.delete(
    "/documents/{document_id}",
    status_code=status.HTTP_204_NO_CONTENT,
)
async def delete_knowledge_document(
    document_id: UUID,
    user: CurrentUser = Depends(get_current_user),
    settings: Settings = Depends(get_request_settings),
    qdrant: AsyncQdrantClient = Depends(get_qdrant),
) -> Response:
    await delete_document_chunks(qdrant, settings.qdrant_collection_name, document_id)

    target_path = find_any_document_file(settings, document_id)
    if target_path and target_path.is_file():
        remove_uploaded_document(target_path)

    cache_dir = settings.storage_root / "cache" / "pages"
    if cache_dir.is_dir():
        for item in cache_dir.glob(f"{document_id}_p*.png"):
            item.unlink(missing_ok=True)

    return Response(status_code=status.HTTP_204_NO_CONTENT)


@router.get("/documents/{document_id}/pages/{page}/image")
async def get_knowledge_document_page_image(
    document_id: UUID,
    page: int,
    settings: Settings = Depends(get_request_settings),
) -> FileResponse:
    target_path = find_any_document_file(settings, document_id, "pdf")
    if not target_path or not target_path.is_file():
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Không tìm thấy tài liệu PDF hoặc trang xem trước.",
        )

    cache_dir = settings.storage_root / "cache" / "pages"
    try:
        image_path = render_pdf_page_image(target_path, page, cache_dir)
    except ValueError as error:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=str(error),
        ) from error

    return FileResponse(image_path, media_type="image/png")


@router.get(
    "/documents/{document_id}/outline",
    response_model=list[DocumentOutlineItem],
)
async def get_knowledge_document_outline(
    document_id: UUID,
    user: CurrentUser = Depends(get_current_user),
    settings: Settings = Depends(get_request_settings),
) -> list[DocumentOutlineItem]:
    target_path = find_any_document_file(settings, document_id, "pdf")
    if not target_path or not target_path.is_file():
        return []

    items = get_pdf_outline(target_path)
    return [DocumentOutlineItem.model_validate(item) for item in items]
