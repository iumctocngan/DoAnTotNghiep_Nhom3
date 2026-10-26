from uuid import UUID

from fastapi import APIRouter, Depends, HTTPException
from qdrant_client import AsyncQdrantClient

from api.dependencies import get_qdrant, get_request_settings
from api.schemas import ChatRequest, ChatResponse, CitationResponse
from api.security import CurrentUser, get_current_user
from core import Settings
from generation.chat import answer_question
from generation.gemini import GeminiConfigurationError, GeminiGenerationError

router = APIRouter(tags=["chat"])


@router.post("/notebooks/{notebook_id}/chat", response_model=ChatResponse)
async def chat_with_notebook(
    notebook_id: UUID,
    body: ChatRequest,
    user: CurrentUser = Depends(get_current_user),
    settings: Settings = Depends(get_request_settings),
    qdrant: AsyncQdrantClient = Depends(get_qdrant),
) -> ChatResponse:
    try:
        result = await answer_question(
            notebook_id,
            body.question,
            settings,
            qdrant,
            body.document_ids,
        )
    except GeminiConfigurationError as error:
        raise HTTPException(status_code=503, detail=str(error)) from error
    except GeminiGenerationError as error:
        raise HTTPException(status_code=502, detail=str(error)) from error

    citations = [
        CitationResponse(
            chunk_id=c.chunk_id,
            document_id=c.document_id,
            document_name=c.document_name,
            label=c.label,
            page_number=c.page_number,
            section_title=c.section_title,
            score=c.score,
            image_url=(
                f"{settings.api_v1_prefix}/notebooks/{notebook_id}/documents/{c.document_id}/pages/{c.page_number}/image"
                if c.page_number is not None
                else None
            ),
        )
        for c in result.citations
    ]

    return ChatResponse(
        answer=result.answer,
        citations=citations,
    )

