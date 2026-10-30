from functools import lru_cache
from pathlib import Path

from pydantic import Field, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

MODULE_ROOT = Path(__file__).resolve().parents[2]


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=MODULE_ROOT / ".env",
        env_file_encoding="utf-8",
        env_prefix="RAG_",
        extra="ignore",
    )

    app_name: str = "CMS Edu RAG Service"
    environment: str = "development"
    api_v1_prefix: str = "/api/v1"
    qdrant_url: str = "http://localhost:6333"
    qdrant_collection_name: str = "document_chunks"
    qdrant_cache_collection_name: str = "semantic_cache"
    cache_similarity_threshold: float = 0.95
    cache_ttl_days: int = 14
    storage_root: Path = Path("storage")
    jwt_issuer: str
    jwt_audience: str
    jwt_secret: str
    gemini_api_key: str = Field(min_length=1)

    @model_validator(mode="after")
    def validate_settings(self) -> "Settings":
        if not self.storage_root.is_absolute():
            self.storage_root = (MODULE_ROOT / self.storage_root).resolve()
        if len(self.jwt_secret.encode("utf-8")) < 32:
            raise ValueError("Mã bí mật JWT phải có độ dài tối thiểu 32 bytes.")
        if not self.jwt_issuer.strip() or not self.jwt_audience.strip():
            raise ValueError("Cấu hình JWT issuer và audience phải được thiết lập.")
        return self


@lru_cache
def get_settings() -> Settings:
    return Settings()
