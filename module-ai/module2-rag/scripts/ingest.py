import asyncio
import json
from pathlib import Path
import sys
import uuid

try:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except (AttributeError, ValueError):
    pass

PROJECT_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PROJECT_ROOT / "src"))

from qdrant_client import models

from core import Settings
from ingestion.pipeline import ingest_document
from db import ensure_collection_exists, get_qdrant_client

CLI_NAMESPACE_ID = uuid.UUID("3fa85f64-5717-4562-b3fc-2c963f66afa6")
SUPPORTED_EXTENSIONS = {".pdf", ".docx"}


async def run() -> None:
    raw_dir = PROJECT_ROOT / "data" / "raw"
    processed_dir = PROJECT_ROOT / "data" / "processed"
    paths = sorted(
        path
        for path in raw_dir.iterdir()
        if path.is_file() and path.suffix.lower() in SUPPORTED_EXTENSIONS
    )
    if not paths:
        raise RuntimeError(f"Không tìm thấy file PDF hoặc DOCX nào trong {raw_dir}")

    settings = Settings()
    qdrant = get_qdrant_client(settings.qdrant_url)
    await ensure_collection_exists(qdrant, settings.qdrant_collection_name)

    report_files: list[dict[str, object]] = []
    total_chunks = 0
    report_path = processed_dir / "ingest-report.json"

    print(f"📥 Bắt đầu nạp {len(paths)} tài liệu vào Qdrant từ: {raw_dir}")
    for path in paths:
        doc_id = uuid.uuid5(CLI_NAMESPACE_ID, path.name)
        doc_type = path.suffix.lower().lstrip(".")
        print(f"   • {path.name} ...", end=" ", flush=True)

        chunks_count = await ingest_document(
            document_id=doc_id,
            document_filename=path.name,
            storage_path=path,
            document_type=doc_type,
            settings=settings,
            client=qdrant,
        )
        total_chunks += chunks_count

        records, _ = await qdrant.scroll(
            collection_name=settings.qdrant_collection_name,
            scroll_filter=models.Filter(
                must=[
                    models.FieldCondition(
                        key="document_id",
                        match=models.MatchValue(value=str(doc_id)),
                    )
                ]
            ),
            limit=1000,
            with_payload=True,
            with_vectors=False,
        )
        sorted_records = sorted(
            records, key=lambda r: (r.payload or {}).get("chunk_index", 0)
        )
        points_detail = [
            {
                "id": str(r.id),
                "chunk_index": (r.payload or {}).get("chunk_index"),
                "page_number": (r.payload or {}).get("page_number"),
                "section_title": (r.payload or {}).get("section_title"),
                "text": (r.payload or {}).get("text"),
            }
            for r in sorted_records
        ]

        report_files.append(
            {
                "document_id": str(doc_id),
                "name": path.name,
                "chunks_count": chunks_count,
                "points": points_detail,
            }
        )
        print(f"{chunks_count} chunks")

    processed_dir.mkdir(parents=True, exist_ok=True)
    report_path.write_text(
        json.dumps(
            {
                "documents": report_files,
                "total_chunks": total_chunks,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    print(f"✅ Hoàn tất: {len(report_files)} tài liệu, tổng cộng {total_chunks} chunk.")
    print(f"📄 Báo cáo chi tiết đã lưu tại: {report_path}")


def main() -> None:
    try:
        asyncio.run(run())
    except OSError as error:
        print(
            "❌ Không kết nối được Qdrant. Hãy kiểm tra container Qdrant trước.",
            file=sys.stderr,
        )
        raise SystemExit(1) from error
    except Exception as error:
        print(f"❌ Quá trình nạp tài liệu thất bại: {error}", file=sys.stderr)
        raise SystemExit(1) from error


if __name__ == "__main__":
    main()
