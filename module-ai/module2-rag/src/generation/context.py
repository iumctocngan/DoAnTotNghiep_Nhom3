from retrieval.queries import Citation, SearchHit


def _label(hit: SearchHit) -> str:
    parts = [hit.document_filename]
    if hit.chunk.page_number is not None:
        parts.append(f"trang {hit.chunk.page_number}")
    if hit.chunk.section_title:
        parts.append(hit.chunk.section_title)
    return " - ".join(parts)


def make_citation(hit: SearchHit) -> Citation:
    chunk = hit.chunk
    return Citation(
        chunk_id=chunk.id,
        document_id=chunk.document_id,
        document_name=hit.document_filename,
        label=_label(hit),
        page_number=chunk.page_number,
        section_title=chunk.section_title,
        score=hit.citation_score,
    )


def build_context(hits: list[SearchHit]) -> str:
    return "\n\n".join(
        f"[Nguồn {index}: {_label(hit)}]\n{hit.chunk.text}"
        for index, hit in enumerate(hits, start=1)
    )
