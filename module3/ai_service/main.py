"""
CMS EDU AI Service — Module 3
Entry point FastAPI application.
"""
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from ai_service.config import settings
from ai_service.routers import prediction, cskh


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Lifecycle: khởi động và tắt dịch vụ."""
    print(f"[START] CMS EDU AI Service khoi dong — debug={settings.debug}")
    yield
    print("[STOP] CMS EDU AI Service dung.")


app = FastAPI(
    title="CMS EDU AI Service — Module 3",
    description=(
        "Dịch vụ AI hỗ trợ dự đoán tái phí học viên (ML Renewal Engine) "
        "và chatbot CSKH tổng hợp ngữ cảnh học sinh (LLM Chatbot Agent)."
    ),
    version="1.0.0",
    lifespan=lifespan,
    docs_url="/docs" if True else None,
)

# CORS — cho phép .NET backend và ReactJS frontend gọi vào
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Health check
@app.get("/health", tags=["System"])
def health_check():
    """Kiểm tra dịch vụ AI còn sống."""
    return {"status": "ok", "service": "CMS EDU AI Service", "version": "1.0.0"}


# Routers
app.include_router(prediction.router, prefix="/api/ml", tags=["ML Renewal Prediction"])
app.include_router(cskh.router, prefix="/api/ai", tags=["CSKH Chatbot Context"])
