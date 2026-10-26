from api.chat import router as chat_router
from api.clo_rubric import router as clo_rubric_router
from api.documents import router as documents_router

__all__ = [
    "chat_router",
    "clo_rubric_router",
    "documents_router",
]
