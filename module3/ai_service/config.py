"""
Cấu hình ứng dụng từ biến môi trường (.env).
Dùng pydantic-settings để validate và type-safe.
Tự động tìm kiếm file .env tại thư mục module3 hoặc root workspace.
"""
import os
from typing import Any
from pydantic import field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

_module3_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_env_files = (
    os.path.join(_module3_dir, ".env"),
    os.path.abspath(".env"),
)


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=_env_files,
        env_file_encoding="utf-8",
        extra="ignore",
    )

    # Database
    db_connection_string: str = (
        "DRIVER={ODBC Driver 17 for SQL Server};"
        "SERVER=.\\SQLEXPRESS;DATABASE=CmsEduDb;"
        "Trusted_Connection=Yes;TrustServerCertificate=Yes;"
    )

    # LLM
    gemini_api_key: str = ""
    openai_api_key: str = ""
    llm_provider: str = "gemini"           # "gemini" hoặc "openai"
    llm_model: str = "gemini-flash-lite-latest"

    # ML Model
    ml_model_path: str = "./ai_service/ml_models/renewal_model.joblib"
    ml_model_version: str = "v1.0.0-lgbm"

    # Cache & Token Optimization (CSKH Chatbot)
    enable_chatbot_cache: bool = True
    chatbot_cache_ttl_seconds: int = 300
    chatbot_cache_max_entries: int = 2048

    # App
    debug: bool = True
    log_level: str = "INFO"

    @field_validator("debug", mode="before")
    @classmethod
    def parse_debug(cls, v: Any) -> bool:
        if isinstance(v, str):
            return v.lower() in ("true", "1", "yes", "on", "debug")
        return bool(v)


settings = Settings()
