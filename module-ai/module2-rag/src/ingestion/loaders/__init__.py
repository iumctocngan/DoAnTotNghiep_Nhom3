from pathlib import Path

from ingestion.loaders.docx import extract_docx
from ingestion.loaders.pdf import extract_pdf
from ingestion.loaders.types import ContentUnit, SourceLocation


def extract_document(path: Path) -> list[ContentUnit]:
    suffix = path.suffix.lower()
    if suffix == ".pdf":
        return extract_pdf(path)
    if suffix == ".docx":
        return extract_docx(path)
    raise ValueError(f"Định dạng tài liệu không được hỗ trợ: {suffix}")


__all__ = ["ContentUnit", "SourceLocation", "extract_document"]
