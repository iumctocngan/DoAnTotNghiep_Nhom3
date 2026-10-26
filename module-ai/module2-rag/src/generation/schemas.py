from typing import Literal
from uuid import UUID

from pydantic import BaseModel, Field


class GeneratedRubricLevel(BaseModel):
    name: str
    minimum_score: float
    maximum_score: float
    description: str


class GeneratedRubricCriterion(BaseModel):
    title: str
    description: str
    levels: list[GeneratedRubricLevel]


class GeneratedLearningOutcome(BaseModel):
    code: str
    description: str
    source_chunk_ids: list[UUID]
    criteria: list[GeneratedRubricCriterion]


class GeneratedRubricLevelPayload(BaseModel):
    description: str


class GeneratedRubricCriterionPayload(BaseModel):
    title: str
    description: str
    levels: list[GeneratedRubricLevelPayload] = Field(min_length=5, max_length=5)


class GeneratedLearningOutcomePayload(BaseModel):
    code: str
    description: str
    source_ids: list[int] = Field(default_factory=list)
    criteria: list[GeneratedRubricCriterionPayload] = Field(min_length=1)


class GeneratedCloRubricPayload(BaseModel):
    outcomes: list[GeneratedLearningOutcomePayload] = Field(min_length=1, max_length=5)


AnswerStatus = Literal["answered", "insufficient_evidence", "out_of_scope"]


class GeneratedAnswerDraft(BaseModel):
    answer: str = ""
    used_source_ids: list[int] = Field(default_factory=list)
    status: AnswerStatus
