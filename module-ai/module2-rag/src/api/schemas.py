from typing import Annotated, Literal
from uuid import UUID

from pydantic import (
    AfterValidator,
    BaseModel,
    ConfigDict,
    Field,
    model_validator,
)


def validate_required_text(value: str) -> str:
    cleaned = value.strip()
    if not cleaned:
        raise ValueError("Giá trị văn bản không được để trống.")
    return cleaned


RequiredText = Annotated[str, AfterValidator(validate_required_text)]
SelectedDocumentIds = Annotated[list[UUID], Field(min_length=1, max_length=100)]


class IngestionResponse(BaseModel):
    document_id: UUID
    status: Literal["indexed"] = "indexed"
    chunk_count: int


class ChatMessageItem(BaseModel):
    role: Literal["user", "assistant"]
    content: str


class ChatRequest(BaseModel):
    question: RequiredText = Field(min_length=1, max_length=2_000)
    document_ids: SelectedDocumentIds | None = None
    history: list[ChatMessageItem] | None = None


class CitationResponse(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    chunk_id: UUID
    document_id: UUID
    document_name: str
    label: str
    page_number: int | None
    section_title: str | None
    score: float | None
    image_url: str | None = None


class ChatResponse(BaseModel):
    answer: str
    citations: list[CitationResponse]


class GenerateCloRubricRequest(BaseModel):
    document_id: UUID
    outcome_count: int = Field(default=3, ge=1, le=5)
    start_page: int | None = Field(default=None, ge=1)
    end_page: int | None = Field(default=None, ge=1)

    @model_validator(mode="after")
    def validate_page_range(self) -> "GenerateCloRubricRequest":
        if self.start_page is not None and self.end_page is not None:
            if self.start_page > self.end_page:
                raise ValueError("start_page không được lớn hơn end_page.")
        return self


class DocumentOutlineItem(BaseModel):
    title: str
    start_page: int
    end_page: int
