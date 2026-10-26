import unicodedata
from collections.abc import Iterable
from dataclasses import dataclass


@dataclass(frozen=True)
class SourceLocation:
    document_type: str
    page_number: int | None = None
    section_title: str | None = None


@dataclass(frozen=True)
class ContentUnit:
    text: str
    location: SourceLocation


def normalize_text(text: str) -> str:
    cleaned = "".join(
        " " if unicodedata.category(character) == "Cc" else character
        for character in unicodedata.normalize("NFC", text)
    )
    return " ".join(cleaned.split())


def format_table(rows: Iterable[Iterable[str]]) -> str:
    formatted_rows = []
    for row in rows:
        cells = [normalize_text(cell) for cell in row]
        if any(cells):
            formatted_rows.append(" | ".join(cells))
    return "[Bảng]\n" + "\n".join(formatted_rows) if formatted_rows else ""
