import logging
from typing import Any
from uuid import UUID

from generation.gemini import generate

logger = logging.getLogger(__name__)

DEFAULT_TRIGGER_MESSAGES = 10
DEFAULT_KEEP_COUNT = 4


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
    current_summary: str | None = None,
    current_processed_count: int = 0,
    trigger_messages: int = DEFAULT_TRIGGER_MESSAGES,
    keep_recent_count: int = DEFAULT_KEEP_COUNT,
) -> tuple[str, list[dict[str, Any]], int]:
    cached_summary = (current_summary or "").strip()
    processed_count = max(0, current_processed_count)

    if not history:
        return cached_summary, [], processed_count

    if len(history) < trigger_messages:
        return cached_summary, history, processed_count

    cutoff = max(1, len(history) - keep_recent_count)
    while cutoff < len(history) and history[cutoff].get("role") != "user":
        cutoff += 1

    to_summarize = history[:cutoff]
    fresh_pending = history[cutoff:]
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
        new_processed_count = processed_count + len(to_summarize)
        logger.info(
            "Cập nhật Short-term Memory cho phiên %s: tóm tắt thêm %d tin (tổng %d tin)",
            conversation_id or "unknown",
            len(to_summarize),
            new_processed_count,
        )
        return new_summary, fresh_pending, new_processed_count
    except Exception as err:
        logger.warning("Lỗi tóm tắt memory: %s. Giữ nguyên summary cũ.", err)
        return cached_summary, history, processed_count
