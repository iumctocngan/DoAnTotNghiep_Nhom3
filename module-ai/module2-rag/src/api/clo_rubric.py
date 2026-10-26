from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import Response
from qdrant_client import AsyncQdrantClient

from api.dependencies import get_qdrant, get_request_settings
from api.schemas import GenerateCloRubricRequest
from api.security import CurrentUser, get_current_user
from generation.clo_rubric import (
    CloRubricGenerationError,
    generate_clo_rubric,
)
from generation.gemini import GeminiConfigurationError, GeminiGenerationError
from generation.xlsx_export import render_xlsx
from core import Settings

router = APIRouter(prefix="/clo-rubric", tags=["clo-rubric"])


@router.post(
    "/generate",
    response_class=Response,
    responses={
        200: {
            "content": {
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": {}
            }
        }
    },
)
async def generate_clo_rubric_content(
    body: GenerateCloRubricRequest,
    user: CurrentUser = Depends(get_current_user),
    settings: Settings = Depends(get_request_settings),
    qdrant: AsyncQdrantClient = Depends(get_qdrant),
) -> Response:
    try:
        generated = await generate_clo_rubric(
            body.notebook_id,
            body.document_id,
            settings,
            qdrant,
            outcome_count=body.outcome_count,
            start_page=body.start_page,
            end_page=body.end_page,
        )
    except GeminiConfigurationError as error:
        raise HTTPException(status_code=503, detail=str(error)) from error
    except (CloRubricGenerationError, GeminiGenerationError) as error:
        raise HTTPException(status_code=502, detail=str(error)) from error

    filename = f"clo-rubric-{body.document_id}.xlsx"
    return Response(
        content=render_xlsx(generated),
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f'attachment; filename="{filename}"'},
    )
