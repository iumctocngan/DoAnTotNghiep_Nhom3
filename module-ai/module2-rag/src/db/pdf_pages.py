from pathlib import Path
import pymupdf


def render_pdf_page_image(
    pdf_path: Path,
    page_number: int,
    cache_dir: Path,
    dpi: int = 150,
) -> Path:
    cache_path = cache_dir / f"{pdf_path.stem}_p{page_number}.png"
    if cache_path.is_file():
        return cache_path

    doc = pymupdf.open(pdf_path)
    if page_number < 1 or page_number > len(doc):
        raise ValueError(f"Trang {page_number} không hợp lệ (tài liệu có {len(doc)} trang).")

    page = doc.load_page(page_number - 1)
    pix = page.get_pixmap(dpi=dpi)
    cache_dir.mkdir(parents=True, exist_ok=True)
    pix.save(cache_path)
    return cache_path


def get_pdf_outline(pdf_path: Path) -> list[dict]:
    from ingestion.loaders.pdf import _extract_toc_hierarchy

    if not pdf_path.is_file():
        return []

    doc = pymupdf.open(pdf_path)
    toc_entries = _extract_toc_hierarchy(doc)

    if not toc_entries:
        raw_toc = doc.get_toc()
        for item in raw_toc:
            if len(item) >= 3 and item[1]:
                toc_entries.append((int(item[2]), str(item[1]).strip()))

    if not toc_entries:
        return []

    total_pages = len(doc)
    outline = []
    for i, (start_page, title) in enumerate(toc_entries):
        end_page = toc_entries[i + 1][0] - 1 if i + 1 < len(toc_entries) else total_pages
        if end_page < start_page:
            end_page = start_page
        outline.append({
            "title": title,
            "start_page": start_page,
            "end_page": end_page,
        })
    return outline
