from pathlib import Path
from uuid import UUID

from fastapi import HTTPException, UploadFile, status

from core import Settings

MAX_DOCUMENT_BYTES = 25 * 1024 * 1024
GLOBAL_FOLDER = "global"


def get_document_path(
    settings: Settings,
    document_id: UUID | str,
    document_type: str,
) -> Path:
    return settings.storage_root / "uploads" / GLOBAL_FOLDER / f"{document_id}.{document_type}"


def find_any_document_file(
    settings: Settings,
    document_id: UUID | str,
    document_type: str | None = None,
) -> Path | None:
    uploads_dir = settings.storage_root / "uploads"
    if not uploads_dir.is_dir():
        return None
    pattern = f"**/{document_id}.{document_type}" if document_type else f"**/{document_id}.*"
    for p in uploads_dir.glob(pattern):
        if p.is_file():
            return p
    return None


async def save_uploaded_document(
    file: UploadFile,
    settings: Settings,
    document_id: UUID,
    document_type: str,
) -> Path:
    destination = get_document_path(settings, document_id, document_type)
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
