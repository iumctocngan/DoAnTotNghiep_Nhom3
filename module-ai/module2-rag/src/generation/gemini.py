import asyncio
import logging
from google import genai
from google.genai import types
from pydantic import BaseModel

logger = logging.getLogger(__name__)

DEFAULT_MODELS = ("gemini-3.5-flash-lite", "gemini-2.5-flash")

class GeminiConfigurationError(RuntimeError):
    pass

class GeminiGenerationError(RuntimeError):
    pass

_current_key_idx = 0


def _extract_keys(api_key: str | list[str]) -> list[str]:
    if isinstance(api_key, list):
        return [k.strip() for k in api_key if k.strip()]
    return [k.strip() for part in api_key.replace(";", ",").replace("\n", ",").split(",") if (k := part.strip())]


def _mask_key(key: str) -> str:
    if len(key) <= 8:
        return "***"
    return f"{key[:4]}...{key[-4:]}"


async def generate(
    prompt: str,
    *,
    api_key: str | list[str],
    response_schema: type[BaseModel],
    models: tuple[str, ...] = DEFAULT_MODELS,
    request_timeout_ms: int = 45_000,
) -> str:
    global _current_key_idx
    keys = _extract_keys(api_key)
    if not keys:
        raise GeminiConfigurationError("Chưa cấu hình GEMINI_API_KEY cho dịch vụ RAG.")

    total_keys = len(keys)
    last_error: Exception | None = None

    for attempt in range(total_keys):
        active_idx = (_current_key_idx + attempt) % total_keys
        key = keys[active_idx]
        masked = _mask_key(key)

        client = genai.Client(
            api_key=key,
            http_options=types.HttpOptions(timeout=request_timeout_ms),
        )

        key_quota_exhausted = False
        for model in models:
            try:
                response = await client.aio.models.generate_content(
                    model=model,
                    contents=prompt,
                    config=types.GenerateContentConfig(
                        response_mime_type="application/json",
                        response_schema=response_schema,
                    ),
                )
                answer = (response.text or "").strip()
                if answer:
                    _current_key_idx = active_idx
                    return answer
            except Exception as error:
                last_error = error
                err_str = str(error).lower()
                is_quota = "429" in err_str or "resource_exhausted" in err_str or "quota" in err_str
                if is_quota:
                    logger.warning(
                        "API Key [%s] hết hạn mức (429/Quota, model=%s). Tự động chuyển sang key dự phòng tiếp theo...",
                        masked,
                        model,
                    )
                    key_quota_exhausted = True
                    break
                else:
                    logger.warning("Gọi Gemini thất bại model=%s (key=%s): %s", model, masked, error)

        if key_quota_exhausted and total_keys > 1:
            continue

    raise GeminiGenerationError(
        f"Tất cả {total_keys} API Key Gemini đều gặp sự cố hoặc hết hạn mức. ({last_error})"
    )