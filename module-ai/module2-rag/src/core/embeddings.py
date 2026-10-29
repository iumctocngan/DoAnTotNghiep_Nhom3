import asyncio
from functools import lru_cache
from threading import Lock

EMBEDDING_MODEL = "intfloat/multilingual-e5-small"
EMBEDDING_DIMENSION = 384
PASSAGE_PREFIX = "passage: "
QUERY_PREFIX = "query: "

_DENSE_LOCK = Lock()
_SPARSE_LOCK = Lock()


def prepare_document_text(text: str) -> str:
    cleaned = text.strip()
    return f"{PASSAGE_PREFIX}{cleaned}" if cleaned else ""


@lru_cache(maxsize=1)
def _dense_model(model_name: str):
    from sentence_transformers import SentenceTransformer

    try:
        return SentenceTransformer(model_name, device="cpu", local_files_only=True)
    except Exception:
        return SentenceTransformer(model_name, device="cpu")


@lru_cache(maxsize=1)
def _sparse_model():
    from fastembed import SparseTextEmbedding
    return SparseTextEmbedding(model_name="Qdrant/bm25", disable_stemmer=True)


def _encode_dense(model_name: str, texts: list[str]) -> list[list[float]]:
    model = _dense_model(model_name)
    with _DENSE_LOCK:
        vectors = model.encode(texts, normalize_embeddings=True, show_progress_bar=False)
    return vectors.tolist()


def _encode_sparse(texts: list[str]) -> list[tuple[list[int], list[float]]]:
    model = _sparse_model()
    with _SPARSE_LOCK:
        embeddings = list(model.embed(texts))
    return [(e.indices.tolist(), e.values.tolist()) for e in embeddings]


def _encode_sparse_query(texts: list[str]) -> list[tuple[list[int], list[float]]]:
    model = _sparse_model()
    with _SPARSE_LOCK:
        embeddings = list(model.query_embed(texts))
    return [(e.indices.tolist(), e.values.tolist()) for e in embeddings]


async def embed_documents(texts: list[str], model_name: str = EMBEDDING_MODEL) -> list[list[float]]:
    prepared = [prepare_document_text(text) for text in texts]
    return await asyncio.to_thread(_encode_dense, model_name, prepared)


async def embed_sparse_documents(texts: list[str]) -> list[tuple[list[int], list[float]]]:
    return await asyncio.to_thread(_encode_sparse, texts)


async def embed_hybrid_query(
    query: str, model_name: str = EMBEDDING_MODEL
) -> tuple[list[float], list[int], list[float]]:
    dense, sparse = await asyncio.gather(
        asyncio.to_thread(_encode_dense, model_name, [f"{QUERY_PREFIX}{query.strip()}"]),
        asyncio.to_thread(_encode_sparse_query, [query]),
    )
    indices, values = sparse[0]
    return dense[0], indices, values
