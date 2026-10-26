from pathlib import Path
from uuid import UUID

from fastapi import HTTPException, UploadFile, status

from core import Settings

MAX_DOCUMENT_BYTES = 25 * 1024 * 1024


def notebook_upload_dir(settings: Settings, notebook_id: UUID | str) -> Path:
    return settings.storage_root / "uploads" / str(notebook_id)


def document_path(
    settings: Settings,
    notebook_id: UUID | str,
    document_id: UUID | str,
    document_type: str,
) -> Path:
    return notebook_upload_dir(settings, notebook_id) / f"{document_id}.{document_type}"


async def save_uploaded_document(
    file: UploadFile,
    settings: Settings,
    notebook_id: UUID,
    document_id: UUID,
    document_type: str,
) -> Path:
    destination = document_path(settings, notebook_id, document_id, document_type)
    destination.parent.mkdir(parents=True, exist_ok=True)

    size = 0
    with destination.open("wb") as output:
        while chunk := await file.read(1024 * 1024):
            size += len(chunk)
            if size > MAX_DOCUMENT_BYTES:
                destination.unlink(missing_ok=True)
                raise HTTPException(
                    status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
                    detail="Dung lượng tài liệu không được vượt quá 25MB.",
                )
            output.write(chunk)

    if size == 0:
        destination.unlink(missing_ok=True)
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="Tài liệu tải lên không được để rỗng.",
        )

    return destination


def remove_uploaded_document(path: Path) -> None:
    path.unlink(missing_ok=True)
    parent = path.parent
    try:
        if parent.is_dir() and not any(parent.iterdir()):
            parent.rmdir()
    except OSError:
        pass
