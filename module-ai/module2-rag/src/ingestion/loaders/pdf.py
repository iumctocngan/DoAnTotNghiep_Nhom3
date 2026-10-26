import re
from pathlib import Path

import pymupdf

from ingestion.loaders.types import ContentUnit, SourceLocation, normalize_text

_HEADER_ZONE_RATIO = 0.12
_FOOTER_ZONE_RATIO = 0.93
_MIN_HEADING_FONT_SIZE = 14.0


def extract_pdf(path: Path) -> list[ContentUnit]:
    units: list[ContentUnit] = []
    with pymupdf.open(path) as document:
        toc_map = _extract_toc_hierarchy(document)
        active_activity: str | None = None
        current_lesson: str | None = None

        for page_number, page in enumerate(document, start=1):
            lesson = _resolve_major_lesson(toc_map, page_number)
            if lesson != current_lesson:
                active_activity = None
                current_lesson = lesson

            active_activity = _extract_activity_banner_title(page) or active_activity
            section_title = " > ".join(part for part in (lesson, active_activity) if part) or None

            footer_limit = page.rect.height * _FOOTER_ZONE_RATIO
            page_lines = [
                cleaned
                for block in page.get_text("blocks")
                if block[1] < footer_limit and (cleaned := normalize_text(block[4]))
            ]
            if page_lines:
                units.append(
                    ContentUnit(
                        " ".join(page_lines),
                        SourceLocation("pdf", page_number, section_title),
                    )
                )

    if not units:
        raise ValueError("File PDF không có văn bản hoặc là file scan chỉ chứa hình ảnh.")
    return units


# Bố cục riêng của sách CMS Edu: mục lục nằm ở trang 3 (index 2), banner hoạt động ở 12% đầu trang.
def _extract_toc_hierarchy(document: pymupdf.Document) -> list[tuple[int, str]]:
    if len(document) < 3:
        return []
    toc_page = document[2]
    blocks = toc_page.get_text("blocks")
    entries: list[tuple[int, str]] = []
    for block in blocks:
        if len(block) < 5:
            continue
        text = block[4].strip()
        match = re.search(r"(Bài\s+\d+)", text, re.IGNORECASE)
        if not match:
            continue
        prefix = match.group(1).strip()
        remaining = text[:match.start()] + text[match.end():]
        page_match = re.search(r"\b(\d{1,3})\b", remaining)
        if not page_match:
            continue
        page_number = int(page_match.group(1))
        title_raw = remaining[:page_match.start()] + remaining[page_match.end():]
        clean_title = re.sub(r"\s+", " ", title_raw).strip(" :-\t\n\r")
        full_lesson = f"{prefix}: {clean_title}" if clean_title else prefix
        entries.append((page_number, full_lesson))
    entries.sort(key=lambda item: item[0])
    return entries


def _resolve_major_lesson(toc_map: list[tuple[int, str]], page_number: int) -> str | None:
    matched_lesson: str | None = None
    for start_page, title in toc_map:
        if page_number >= start_page:
            matched_lesson = title
        else:
            break
    return matched_lesson


def _extract_activity_banner_title(page: pymupdf.Page) -> str | None:
    header_rect = pymupdf.Rect(0, 0, page.rect.width, page.rect.height * _HEADER_ZONE_RATIO)
    header_dict = page.get_text("dict", clip=header_rect)
    max_font_size = 0.0
    for block in header_dict.get("blocks", []):
        for line in block.get("lines", []):
            for span in line.get("spans", []):
                span_size = float(span.get("size", 0.0))
                if span_size > max_font_size:
                    max_font_size = span_size
    if max_font_size < _MIN_HEADING_FONT_SIZE:
        return None
    raw_text = page.get_text("text", clip=header_rect).strip()
    if not raw_text:
        return None
    clean_text = re.sub(r"\s+", " ", raw_text).strip()
    if clean_text.casefold() in {"cms edu", "black hole"}:
        return None
    return clean_text if len(clean_text) >= 3 else None