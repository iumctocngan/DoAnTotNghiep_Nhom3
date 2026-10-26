from pathlib import Path

import docx

from ingestion.loaders.types import (
    ContentUnit,
    SourceLocation,
    format_table,
    normalize_text,
)


def extract_docx(path: Path) -> list[ContentUnit]:
    document = docx.Document(path)
    units: list[ContentUnit] = []
    active_heading: str | None = None

    for child in document.element.body.iterchildren():
        if child.tag.endswith("p"):
            paragraph = docx.text.paragraph.Paragraph(child, document)
            text = normalize_text(paragraph.text)
            if not text:
                continue
            style_name = (paragraph.style.name or "").casefold()
            if style_name.startswith(("heading", "tiêu đề")):
                active_heading = text
            units.append(ContentUnit(text, SourceLocation("docx", section_title=active_heading)))
        elif child.tag.endswith("tbl"):
            table = docx.table.Table(child, document)
            table_text = format_table([cell.text for cell in row.cells] for row in table.rows)
            if table_text:
                units.append(ContentUnit(table_text, SourceLocation("docx", section_title=active_heading)))

    if not units:
        raise ValueError("File DOCX không chứa văn bản hợp lệ.")
    return units
