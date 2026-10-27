import asyncio
import json
import logging
import os
import sys
import time
import warnings
from uuid import UUID

warnings.filterwarnings("ignore", category=DeprecationWarning)
warnings.filterwarnings("ignore", category=UserWarning)

import matplotlib
import matplotlib.pyplot as plt
import numpy as np
from langchain_google_genai import ChatGoogleGenerativeAI
from ragas import EvaluationDataset, evaluate
from ragas.embeddings import HuggingFaceEmbeddings
from ragas.metrics import answer_correctness, context_recall, faithfulness
from ragas.llms import LangchainLLMWrapper

sys.stdout.reconfigure(encoding="utf-8")
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(levelname)-8s | %(message)s",
    handlers=[logging.StreamHandler(sys.stdout)],
)
logger = logging.getLogger("Ragas_CMS_Eval")

DATA_DIR = os.path.join(os.path.dirname(__file__), "..", "data", "benchmark")
DATASET_JSON = os.path.join(DATA_DIR, "ground_truth.json")
RESULTS_JSON = os.path.join(DATA_DIR, "eval_results.json")
CHART_PATH = os.path.join(DATA_DIR, "metrics_by_question_type.png")
CACHE_JSON = os.path.join(DATA_DIR, "rag_cache.json")

plt.rcParams["font.sans-serif"] = ["Segoe UI", "Arial", "DejaVu Sans"]
plt.rcParams["axes.unicode_minus"] = False


def load_dataset() -> list[dict]:
    if not os.path.exists(DATASET_JSON):
        raise FileNotFoundError(f"Không tìm thấy file ground_truth.json tại: {DATASET_JSON}")
    with open(DATASET_JSON, "r", encoding="utf-8") as f:
        return json.load(f)


def render_evaluation_chart(results: list[dict], output_path: str):
    """Xuất 1 biểu đồ duy nhất 300 DPI so sánh 3 tiêu chí theo từng dạng câu hỏi."""
    grouped = {}
    for r in results:
        t = r["dang_cau_hoi"]
        grouped.setdefault(t, []).append(r)

    active_types = [t for t in ["Giải toán & Suy luận", "Khái niệm & Định nghĩa"] if t in grouped]
    labels = [f"{t}\n({len(grouped[t])} câu)" for t in active_types]

    recalls = [float(np.nanmean([x["context_recall"] for x in grouped[t]])) * 10 for t in active_types]
    faiths = [float(np.nanmean([x["faithfulness"] for x in grouped[t]])) * 10 for t in active_types]
    corrs = [float(np.nanmean([x["answer_correctness"] for x in grouped[t]])) * 10 for t in active_types]

    x = np.arange(len(active_types))
    width = 0.22

    fig, ax = plt.subplots(figsize=(8.8, 5.0), dpi=300)
    rects1 = ax.bar(x - width, recalls, width, label="Retrieval (Context Recall)", color="#2563EB", alpha=0.9)
    rects2 = ax.bar(x, faiths, width, label="Bám nguồn (Faithfulness)", color="#10B981", alpha=0.9)
    rects3 = ax.bar(x + width, corrs, width, label="Chính xác (Correctness)", color="#8B5CF6", alpha=0.9)

    ax.set_ylabel("Điểm chuẩn hóa (Thang 0 - 10)", fontsize=11, fontweight="bold")
    ax.set_title("ĐÁNH GIÁ RAG THEO CHUẨN RAGAS (METRICS BY QUESTION TYPE)", fontsize=12.5, fontweight="bold", pad=28)
    ax.set_xticks(x)
    ax.set_xticklabels(labels, fontsize=10.5, fontweight="bold")
    ax.set_ylim(0, 11.5)
    ax.legend(loc="upper center", bbox_to_anchor=(0.5, 1.08), ncol=3, frameon=False, fontsize=9.5)
    ax.grid(axis="y", linestyle="--", alpha=0.35)
    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)

    for rect in list(rects1) + list(rects2) + list(rects3):
        h = rect.get_height()
        ax.annotate(
            f"{h:.1f}",
            xy=(rect.get_x() + rect.get_width() / 2, h),
            xytext=(0, 3),
            textcoords="offset points",
            ha="center",
            va="bottom",
            fontsize=8.5,
            fontweight="bold",
        )

    plt.tight_layout()
    plt.savefig(output_path, dpi=300)
    plt.close()

    print(f"\n[Biểu đồ trực quan] Đã xuất thành công biểu đồ chuẩn ấn phẩm 300 DPI tại:")
    print(f"  • {output_path}")


def print_terminal_report(results: list[dict], elapsed: float):
    total = len(results)
    if not total:
        return

    domain_r = [r for r in results if r["dang_cau_hoi"] != "Câu hỏi bẫy"]
    trick_r = [r for r in results if r["dang_cau_hoi"] == "Câu hỏi bẫy"]

    avg_recall = float(np.nanmean([r["context_recall"] for r in domain_r])) * 10 if domain_r else 0.0
    avg_faith = float(np.nanmean([r["faithfulness"] for r in domain_r])) * 10 if domain_r else 0.0
    avg_corr = float(np.nanmean([r["answer_correctness"] for r in domain_r])) * 10 if domain_r else 0.0

    avg_unans = (
        float(np.mean([10.0 if r["answer_correctness"] >= 0.75 else 0.0 for r in trick_r])) if trick_r else 10.0
    )

    sep = "+" + "-" * 32 + "+" + "-" * 15 + "+" + "-" * 16 + "+"
    print("\n" + sep)
    print(f"| {'KẾT QUẢ ĐÁNH GIÁ RAGAS BENCHMARK':^65} |")
    print(sep)
    print(f"| {'I. HIỆU NĂNG RAG TOÁN CMS':<30} | {f'({len(domain_r)} câu)':<13} | {' ':^14} |")
    print(f"| {'  • Retrieval (Context Recall)':<30} | {avg_recall:>11.2f}   | {avg_recall*10:>12.1f}%  |")
    print(f"| {'  • Bám nguồn (Faithfulness)':<30} | {avg_faith:>11.2f}   | {avg_faith*10:>12.1f}%  |")
    print(f"| {'  • Chính xác (Correctness)':<30} | {avg_corr:>11.2f}   | {avg_corr*10:>12.1f}%  |")
    if trick_r:
        print(sep)
        print(f"| {'II. ĐỘ AN TOÀN & KHÁNG BẪY':<30} | {f'({len(trick_r)} câu)':<13} | {' ':^14} |")
        print(f"| {'  • Từ chối an toàn (Abstention)':<30} | {avg_unans:>11.2f}   | {avg_unans*10:>12.1f}%  |")

    print(sep)
    print(f"| Thời gian chạy: {elapsed:.1f}s | Tổng số câu hỏi: {total} câu{' ':>24} |")
    print(sep)


async def _async_collect_missing(missing_items: list[dict], top_k: int, notebook_id: UUID, settings) -> dict:
    from generation.chat import answer_question
    from qdrant_client import AsyncQdrantClient
    from retrieval import search_notebook_context

    client = AsyncQdrantClient(url=settings.qdrant_url)
    results = {}
    for idx, item in enumerate(missing_items, start=1):
        qid = item["ma_cau_hoi"]
        q = item["cau_hoi"]
        print(f"[{idx:02d}/{len(missing_items)}] {qid}: Đang sinh câu trả lời RAG...", end=" ", flush=True)
        retrieved_hits = await search_notebook_context(
            notebook_id, q, top_k=top_k, settings=settings, client=client
        )
        contexts = [h.chunk.text for h in retrieved_hits] or ["Tôi chưa tìm thấy thông tin phù hợp."]
        rag_answer = await answer_question(notebook_id, q, settings, client)
        results[qid] = {
            "user_input": q,
            "retrieved_contexts": contexts,
            "response": rag_answer.answer,
            "reference": item["dap_an_chuan"],
            "ma_cau_hoi": qid,
            "dang_cau_hoi": item["dang_cau_hoi"],
        }
        print("-> Xong")
        await asyncio.sleep(0.5)
    return results


def run_evaluation(top_k: int = 5):
    from core import get_settings
    settings = get_settings()

    ingest_report_path = os.path.join(os.path.dirname(__file__), "..", "data", "processed", "ingest-report.json")
    with open(ingest_report_path, "r", encoding="utf-8") as f:
        ingest_data = json.load(f)
    notebook_id = UUID(ingest_data["notebook_id"])

    dataset = load_dataset()
    total_q = len(dataset)

    # 1. Quản lý Cache RAG answers
    cached_map = {}
    use_cache = "--fresh" not in sys.argv and "--no-cache" not in sys.argv
    if use_cache and os.path.exists(CACHE_JSON):
        try:
            with open(CACHE_JSON, "r", encoding="utf-8") as f:
                cached_items = json.load(f)
                cached_map = {item["ma_cau_hoi"]: item for item in cached_items if "response" in item}
        except Exception as e:
            logger.warning("Không thể đọc cache: %s", e)
            cached_map = {}

    missing = [item for item in dataset if item["ma_cau_hoi"] not in cached_map]
    if missing:
        print(f"Cần thu thập {len(missing)} câu trả lời RAG chưa có trong cache...")
        new_collected = asyncio.run(_async_collect_missing(missing, top_k, notebook_id, settings))
        cached_map.update(new_collected)
        with open(CACHE_JSON, "w", encoding="utf-8") as f:
            json.dump(list(cached_map.values()), f, ensure_ascii=False, indent=2)
    else:
        print(f"[Cache RAG] Đã có đủ {total_q}/{total_q} câu trả lời trong '{os.path.basename(CACHE_JSON)}'.")

    # Tạo danh sách collected_data theo đúng thứ tự dataset
    collected_data = []
    for item in dataset:
        qid = item["ma_cau_hoi"]
        entry = cached_map[qid]
        entry["user_input"] = item["cau_hoi"]
        entry["reference"] = item["dap_an_chuan"]
        entry["dang_cau_hoi"] = item["dang_cau_hoi"]
        collected_data.append(entry)

    # 2. Cấu hình Evaluator LLM, Embeddings & Metrics
    keys = [k.strip() for k in settings.gemini_api_key.split(",") if k.strip()]
    if not keys:
        raise ValueError("Chưa cấu hình GEMINI_API_KEY trong file .env")

    current_key_idx = 0
    eval_embeddings = HuggingFaceEmbeddings(model="intfloat/multilingual-e5-small")

    ac_metric = answer_correctness
    ac_metric.weights = [0.4, 0.6]
    ac_metric.beta = 2.0
    ac_metric.embeddings = eval_embeddings
    metrics_list = [context_recall, faithfulness, ac_metric]

    # Kiểm tra RESULTS_JSON để phục hồi câu đã chấm trước đó
    evaluated_map = {}
    if os.path.exists(RESULTS_JSON):
        try:
            with open(RESULTS_JSON, "r", encoding="utf-8") as f:
                old_results = json.load(f)
                evaluated_map = {
                    r["ma_cau_hoi"]: r
                    for r in old_results
                    if "answer_correctness" in r
                    and not np.isnan(r.get("context_recall", 0.0))
                    and not np.isnan(r.get("faithfulness", 0.0))
                    and not np.isnan(r.get("answer_correctness", 0.0))
                }
        except Exception:
            evaluated_map = {}

    print("\n" + "=" * 80)
    print(f"[Ragas] BẮT ĐẦU CHẤM ĐIỂM TUẦN TỰ {total_q} CÂU (MODEL: gemini-3.5-flash-lite, TỰ XOAY VÒNG {len(keys)} KEYS)")
    print("=" * 80)

    start_time = time.time()
    final_results = []

    for idx, item in enumerate(collected_data, start=1):
        qid = item["ma_cau_hoi"]
        q_type = item["dang_cau_hoi"]

        if qid in evaluated_map:
            print(f"[{idx:02d}/{total_q}] {qid} ({q_type:<12}): [Đã có điểm đánh giá từ trước]")
            final_results.append(evaluated_map[qid])
            continue

        if q_type == "Câu hỏi bẫy":
            resp_lower = item["response"].lower()
            is_refused = any(k in resp_lower for k in [
                "chưa tìm thấy", "ngoài phạm vi", "không tìm thấy", "không có thông tin", "nằm ngoài", "không có trong"
            ])
            ref_score = 1.0 if is_refused else 0.0
            record = {
                "ma_cau_hoi": qid,
                "dang_cau_hoi": q_type,
                "cau_hoi": item["user_input"],
                "dap_an_chuan": item["reference"],
                "cau_tra_loi_rag": item["response"][:200],
                "context_recall": 1.0 if is_refused else 0.0,
                "faithfulness": 1.0 if is_refused else 0.0,
                "answer_correctness": ref_score,
            }
            final_results.append(record)
            evaluated_map[qid] = record
            with open(RESULTS_JSON, "w", encoding="utf-8") as f:
                json.dump(list(evaluated_map.values()), f, ensure_ascii=False, indent=2)
            status_text = "Từ chối an toàn (Đạt)" if is_refused else "Ảo giác (Hỏng)"
            print(f"[{idx:02d}/{total_q}] {qid} ({q_type:<12}): -> {status_text} | Kháng ảo giác: {ref_score*10:.1f}")
            continue

        print(f"[{idx:02d}/{total_q}] {qid} ({q_type:<12}): Đang chấm điểm...", end=" ", flush=True)

        scored = False
        max_attempts = len(keys) * 2

        for attempt in range(max_attempts):
            active_key = keys[current_key_idx]
            raw_llm = ChatGoogleGenerativeAI(
                model="gemini-3.5-flash-lite",
                google_api_key=active_key,
                temperature=0.0,
            )
            evaluator_llm = LangchainLLMWrapper(raw_llm)

            sample_ds = EvaluationDataset.from_list(
                [
                    {
                        "user_input": item["user_input"],
                        "retrieved_contexts": item["retrieved_contexts"],
                        "response": item["response"],
                        "reference": item["reference"],
                    }
                ]
            )

            try:
                eval_res = evaluate(
                    dataset=sample_ds,
                    metrics=metrics_list,
                    llm=evaluator_llm,
                    embeddings=eval_embeddings,
                    show_progress=False,
                )
                row = eval_res.to_pandas().iloc[0]
                rec = float(row.get("context_recall", 0.0) or 0.0)
                fth = float(row.get("faithfulness", 0.0) or 0.0)
                cor = float(row.get("answer_correctness", 0.0) or 0.0)

                if np.isnan(rec) or np.isnan(fth) or np.isnan(cor):
                    print(f"-> [Phát hiện NaN: {rec=}, {fth=}, {cor=}] Chuyển Key để chấm lại câu {qid}...")
                    current_key_idx = (current_key_idx + 1) % len(keys)
                    time.sleep(1.0)
                    continue

                record = {
                    "ma_cau_hoi": qid,
                    "dang_cau_hoi": q_type,
                    "cau_hoi": item["user_input"],
                    "dap_an_chuan": item["reference"],
                    "cau_tra_loi_rag": item["response"][:200],
                    "context_recall": rec,
                    "faithfulness": fth,
                    "answer_correctness": cor,
                }
                final_results.append(record)
                evaluated_map[qid] = record

                with open(RESULTS_JSON, "w", encoding="utf-8") as f:
                    json.dump(list(evaluated_map.values()), f, ensure_ascii=False, indent=2)

                print(f"-> Recall: {rec*10:>4.1f} | Faith: {fth*10:>4.1f} | Corr: {cor*10:>4.1f} (Key {current_key_idx+1})")
                scored = True

                if idx % 3 == 0:
                    current_key_idx = (current_key_idx + 1) % len(keys)
                time.sleep(2.5)
                break

            except Exception as e:
                err_str = str(e)
                if "429" in err_str or "RESOURCE_EXHAUSTED" in err_str or "quota" in err_str.lower():
                    prev_idx = current_key_idx
                    current_key_idx = (current_key_idx + 1) % len(keys)
                    print(f"\n[Quota Exceeded Key {prev_idx+1}] Chuyển ngay sang Key {current_key_idx+1} để chấm tiếp câu {qid}...")
                    time.sleep(1.0)
                else:
                    logger.warning("Lỗi khi chấm câu %s với Key %d: %s", qid, current_key_idx + 1, e)
                    current_key_idx = (current_key_idx + 1) % len(keys)
                    time.sleep(1.0)

        if not scored:
            print("-> Ghi nhận 0.0 do hết quota toàn bộ keys")
            record = {
                "ma_cau_hoi": qid,
                "dang_cau_hoi": q_type,
                "cau_hoi": item["user_input"],
                "dap_an_chuan": item["reference"],
                "cau_tra_loi_rag": item["response"][:200],
                "context_recall": 0.0,
                "faithfulness": 0.0,
                "answer_correctness": 0.0,
            }
            final_results.append(record)
            evaluated_map[qid] = record
            with open(RESULTS_JSON, "w", encoding="utf-8") as f:
                json.dump(list(evaluated_map.values()), f, ensure_ascii=False, indent=2)

    elapsed = time.time() - start_time
    print_terminal_report(final_results, elapsed)
    render_evaluation_chart(final_results, CHART_PATH)


if __name__ == "__main__":
    top_k_val = 5
    for arg in sys.argv[1:]:
        if arg.startswith("--top_k="):
            top_k_val = int(arg.split("=")[1])
        elif arg == "--top_k" and sys.argv.index(arg) + 1 < len(sys.argv):
            top_k_val = int(sys.argv[sys.argv.index(arg) + 1])
    run_evaluation(top_k=top_k_val)
