"""Adapter LLM cho CSKH; không chứa khóa bí mật và không ghi dữ liệu nghiệp vụ."""
import json
import logging

from ai_service.config import settings
from ai_service.schemas.cskh_schemas import CallScriptResponse

logger = logging.getLogger(__name__)


def generate_call_script(prompt: str) -> CallScriptResponse:
    """Gọi provider đã cấu hình và chỉ nhận JSON theo output schema.

    Việc lưu session/message thuộc Core API để Core áp dụng JWT, RBAC và audit.
    Tự động áp dụng cơ chế chuyển giao model dự phòng (fallback) nếu gặp lỗi 503 tạm thời.
    """
    if settings.llm_provider == "openai":
        if not settings.openai_api_key:
            raise RuntimeError("OPENAI_API_KEY chưa được cấu hình")
        from openai import OpenAI
        response = OpenAI(api_key=settings.openai_api_key).chat.completions.create(
            model=settings.llm_model,
            messages=[{"role": "system", "content": prompt}],
            response_format={"type": "json_object"},
            temperature=0,
        )
        content = response.choices[0].message.content
    elif settings.llm_provider == "gemini":
        if not settings.gemini_api_key:
            raise RuntimeError("GEMINI_API_KEY chưa được cấu hình")
        from google import genai
        from google.genai import errors
        from google.genai import types

        models_to_try = [settings.llm_model]
        if "gemini-flash-lite-latest" not in models_to_try:
            models_to_try.append("gemini-flash-lite-latest")
        if "gemini-3.8-flash" not in models_to_try:
            models_to_try.append("gemini-3.8-flash")

        client = genai.Client(api_key=settings.gemini_api_key)
        content = None
        last_exc: Exception | None = None

        for model_name in models_to_try:
            try:
                response = client.models.generate_content(
                    model=model_name,
                    contents=prompt,
                    config=types.GenerateContentConfig(
                        temperature=0,
                        response_mime_type="application/json",
                        response_schema=CallScriptResponse,
                    ),
                )
                content = response.text
                break
            except errors.APIError as exc:
                last_exc = exc
                logger.warning(f"Model {model_name} trả về lỗi HTTP {exc.code}. Đang thử model dự phòng tiếp theo...")
                continue
        else:
            raise RuntimeError(
                f"Gemini API tạm thời không khả dụng ({last_exc}). Vui lòng thử lại sau."
            ) from last_exc
    else:
        raise RuntimeError("LLM_PROVIDER chỉ hỗ trợ 'gemini' hoặc 'openai'")

    try:
        return CallScriptResponse.model_validate(json.loads(content or ""))
    except (json.JSONDecodeError, ValueError) as exc:
        raise RuntimeError("LLM trả về không đúng schema call script") from exc
