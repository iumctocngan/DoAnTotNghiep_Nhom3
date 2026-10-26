from fastapi import Request
from qdrant_client import AsyncQdrantClient

from core import Settings


def get_request_settings(request: Request) -> Settings:
    return request.app.state.settings


def get_qdrant(request: Request) -> AsyncQdrantClient:
    return request.app.state.qdrant
