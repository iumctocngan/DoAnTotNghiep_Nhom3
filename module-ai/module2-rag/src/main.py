import asyncio
import logging
import os
from collections.abc import AsyncGenerator
from contextlib import asynccontextmanager

os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from api.chat import router as chat_router
from api.clo_rubric import router as clo_rubric_router
from api.documents import router as documents_router
from core import Settings, get_settings
from db import (
    ensure_cache_collection_exists,
    ensure_collection_exists,
    get_qdrant_client,
)

logger = logging.getLogger(__name__)


def create_app(settings: Settings | None = None) -> FastAPI:
    active_settings = settings or get_settings()

    @asynccontextmanager
    async def lifespan(application: FastAPI) -> AsyncGenerator[None, None]:
        qdrant = get_qdrant_client(active_settings.qdrant_url)
        application.state.qdrant = qdrant
        try:
            await ensure_collection_exists(qdrant, active_settings.qdrant_collection_name)
            await ensure_cache_collection_exists(qdrant, active_settings.qdrant_cache_collection_name)
        except Exception:
            logger.warning("Chưa thể kết nối tới Qdrant khi khởi động. Sẽ thử lại khi có request.")

        try:
            logger.info("Đang nạp trước model weights (Embedding & Reranker)...")
            from core.embeddings import EMBEDDING_MODEL, _dense_model, _sparse_model
            from retrieval.reranker import RERANKER_MODEL, _get_model

            await asyncio.to_thread(_dense_model, EMBEDDING_MODEL)
            await asyncio.to_thread(_sparse_model)
            await asyncio.to_thread(_get_model, RERANKER_MODEL)
            logger.info("Hoàn tất nạp weights. Server sẵn sàng phục vụ với độ trễ tối thiểu.")
        except Exception:
            logger.warning("Không thể nạp trước model weights khi khởi động. Sẽ nạp trễ khi có request.", exc_info=True)
        try:
            yield
        finally:
            await qdrant.close()

    app = FastAPI(
        title=active_settings.app_name,
        version="0.1.0",
        description="Mô-đun RAG cho CMS Edu.",
        lifespan=lifespan,
    )
    app.state.settings = active_settings

    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    @app.get("/health", tags=["system"], summary="Kiểm tra trạng thái hoạt động")
    def health() -> dict[str, str]:
        return {
            "status": "ok",
            "service": active_settings.app_name,
            "environment": active_settings.environment,
        }

    app.include_router(clo_rubric_router, prefix=active_settings.api_v1_prefix)
    app.include_router(documents_router, prefix=active_settings.api_v1_prefix)
    app.include_router(chat_router, prefix=active_settings.api_v1_prefix)

    return app


app = create_app()


if __name__ == "__main__":
    import uvicorn

    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
