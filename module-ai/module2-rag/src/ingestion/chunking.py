from __future__ import annotations

import re
from dataclasses import dataclass, replace
from functools import lru_cache
from itertools import groupby
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from transformers import PreTrainedTokenizerBase

from core import EMBEDDING_MODEL, prepare_document_text
from ingestion.loaders.types import ContentUnit

CHUNK_SIZE: int = 350
CHUNK_OVERLAP: int = 50

@dataclass(frozen=True)
class ChunkDraft:
    text: str
    source: ContentUnit
    chunk_index: int


def build_chunks(
    units: list[ContentUnit],
    chunk_size: int = CHUNK_SIZE,
    overlap: int = CHUNK_OVERLAP,
    tokenizer_model: str = EMBEDDING_MODEL,
) -> list[ChunkDraft]:
    tokenizer = _get_tokenizer(tokenizer_model)
    if tokenizer.model_max_length < chunk_size:
        raise ValueError("Giới hạn token của chunk vượt quá khả năng của embedding model.")

    chunks: list[ChunkDraft] = []
    for unit in _merge_units_by_scope(units):
        title_prefix = f"{unit.location.section_title}\n" if unit.location.section_title else ""
        for piece in _split_text_by_tokens(unit.text, chunk_size, overlap, tokenizer, title_prefix):
            chunks.append(ChunkDraft(text=piece, source=unit, chunk_index=len(chunks)))
    return chunks


def embedding_text(chunk: ChunkDraft) -> str:
    title = chunk.source.location.section_title
    if title and not chunk.text.casefold().startswith(title.casefold()):
        return f"{title}\n{chunk.text}"
    return chunk.text


def _split_text_by_tokens(
    text: str,
    chunk_size: int,
    overlap: int,
    tokenizer: PreTrainedTokenizerBase,
    prefix: str,
) -> list[str]:
    segments = re.findall(r"\S+\s*", text.strip())
    if not segments:
        return []

    pieces: list[str] = []
    start = 0
    while start < len(segments):
        end = _largest_fitting_end(segments, start, chunk_size, tokenizer, prefix)
        pieces.append("".join(segments[start:end]).rstrip())
        if end == len(segments):
            break

        overlap_start = end
        while overlap_start > start:
            candidate = "".join(segments[overlap_start - 1 : end]).strip()
            if len(tokenizer.encode(candidate, add_special_tokens=False, verbose=False)) > overlap:
                break
            overlap_start -= 1
        start = overlap_start if overlap_start > start else end

    return pieces


def _largest_fitting_end(
    segments: list[str],
    start: int,
    chunk_size: int,
    tokenizer: PreTrainedTokenizerBase,
    prefix: str,
) -> int:
    low = start + 1
    high = len(segments)
    best = start
    while low <= high:
        middle = (low + high) // 2
        candidate = "".join(segments[start:middle]).rstrip()
        # Đo token trên đúng chuỗi sẽ embed (có prefix và tiêu đề) để chunk không vượt giới hạn của model.
        prepared = prepare_document_text(f"{prefix}{candidate}")
        tokens = len(tokenizer.encode(prepared, add_special_tokens=True, verbose=False))
        if tokens <= chunk_size:
            best = middle
            low = middle + 1
        else:
            high = middle - 1
    if best == start:
        return start + 1
    return best


def _merge_units_by_scope(units: list[ContentUnit]) -> list[ContentUnit]:
    merged = []
    for _, group in groupby(units, key=lambda unit: unit.location):
        items = list(group)
        merged.append(replace(items[0], text="\n\n".join(item.text.strip() for item in items)))
    return merged


@lru_cache(maxsize=1)
def _get_tokenizer(model_name: str) -> PreTrainedTokenizerBase:
    from transformers import AutoTokenizer

    try:
        return AutoTokenizer.from_pretrained(model_name, local_files_only=True)
    except OSError:
        return AutoTokenizer.from_pretrained(model_name)
