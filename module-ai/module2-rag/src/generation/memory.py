import logging
from dataclasses import dataclass
from typing import Any
from uuid import UUID

from generation.gemini import generate

logger = logging.getLogger(__name__)


@dataclass
class ConversationMemory:
    summary: str = ""
    processed_count: int = 0


_memory_store: dict[str, ConversationMemory] = {}

DEFAULT_TRIGGER_MESSAGES = 10
DEFAULT_KEEP_COUNT = 4


def _get_cp(thread_id: str | None) -> tuple[str, int]:
    if not thread_id or thread_id not in _memory_store:
        return "", 0
    mem = _memory_store[thread_id]
    return mem.summary, mem.processed_count


def _save_cp(thread_id: str | None, summary: str, count: int) -> None:
    if thread_id and summary:
        _memory_store[thread_id] = ConversationMemory(summary=summary, processed_count=count)
        logger.info("Đã lưu Short-term Memory cho phiên %s (%d tin)", thread_id, count)


def _format_messages_to_text(messages: list[dict[str, Any]]) -> str:
    lines = []
    for m in messages:
        role = "Người dùng" if m.get("role") == "user" else "Trợ lý"
        content = str(m.get("content", "")).strip()
        lines.append(f"{role}: {content}")
    return "\n".join(lines)


async def process_conversation_memory(
    history: list[dict[str, Any]] | None,
    api_key: str | list[str],
    conversation_id: UUID | str | None = None,
    trigger_messages: int = DEFAULT_TRIGGER_MESSAGES,
    keep_recent_count: int = DEFAULT_KEEP_COUNT,
) -> tuple[str, list[dict[str, Any]]]:
    if not history:
        return "", []

    thread_id = str(conversation_id) if conversation_id else None
    cached_summary, processed_count = _get_cp(thread_id)

    pending_messages = history[processed_count:] if processed_count < len(history) else history[-keep_recent_count:]

    if len(pending_messages) < trigger_messages:
        return cached_summary, (history[-keep_recent_count:] if cached_summary else history)

    cutoff = max(1, len(pending_messages) - keep_recent_count)
    while cutoff < len(pending_messages) and pending_messages[cutoff].get("role") != "user":
        cutoff += 1

    to_summarize = pending_messages[:cutoff]
    fresh_pending = pending_messages[cutoff:]
    to_sum_text = _format_messages_to_text(to_summarize)

    if cached_summary:
        prompt = (
            "Dưới đây là tóm tắt ngữ cảnh trước đó và các trao đổi mới của cuộc trò chuyện.\n\n"
            f"Tóm tắt ngữ cảnh trước đó:\n{cached_summary}\n\n"
            f"Các trao đổi mới:\n{to_sum_text}\n\n"
            "Hãy hợp nhất và viết lại MỘT bản tóm tắt ngắn gọn, súc tích (tối đa 150 từ), "
            "giữ lại các dữ kiện, thuật ngữ và công thức quan trọng:\n"
            "Tóm tắt cập nhật:"
        )
    else:
        prompt = (
            f"Hãy tóm tắt ngắn gọn các ý chính của cuộc trao đổi sau trong tối đa 150 từ:\n\n"
            f"{to_sum_text}\n\nTóm tắt:"
        )

    try:
        new_summary = (await generate(prompt, api_key=api_key, request_timeout_ms=10_000)).strip()
        new_processed_count = len(history) - len(fresh_pending)
        _save_cp(thread_id, new_summary, new_processed_count)
        logger.info("Cập nhật Short-term Memory cho phiên %s", thread_id)
        return new_summary, fresh_pending
    except Exception as err:
        logger.warning("Lỗi tóm tắt memory: %s. Giữ nguyên summary cũ.", err)
        return cached_summary, (history[-keep_recent_count:] if cached_summary else history)
