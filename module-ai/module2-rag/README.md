# Module 2: RAG Service — CMS Edu

Dịch vụ RAG (Retrieval-Augmented Generation) phục vụ hỏi đáp thông minh dựa trên giáo trình nội bộ và tự động sinh chuẩn đầu ra (CLO) kèm ma trận Rubric đánh giá năng lực cho hệ thống CMS Edu.

---

## 1. Giới thiệu tổng quan & Tính năng chính

- **Hỏi đáp giáo trình có dẫn chứng (Grounded Document Q&A)**: 
  - Tra cứu nhanh nội dung sách, bài tập chuyên đề toán tư duy theo kho tài liệu học liệu.
  - Trả lời chính xác, kèm số trang trích dẫn và văn bản nguồn làm minh chứng trực tiếp; chủ động báo `insufficient_evidence` hoặc `out_of_scope` khi câu hỏi nằm ngoài phạm vi tài liệu để chống ảo giác (hallucination).
- **Tự động sinh Chuẩn đầu ra (CLO) & Ma trận Rubric**: 
  - Quét phạm vi bài học (in-context window lớn), trích xuất mục tiêu và phân loại hành vi theo 6 cấp độ thang đo tư duy Bloom.
  - Tự động sinh bảng tiêu chí đánh giá năng lực đa cấp độ và xuất trực tiếp ra file Excel (`.xlsx`) phục vụ công tác giảng dạy.

---

## 2. Công nghệ sử dụng

| Thành phần | Công nghệ / Thư viện | Vai trò |
|---|---|---|
| **API Framework** | FastAPI, Uvicorn, Pydantic v2 | Xây dựng RESTful API async hiệu năng cao, kiểm định schema chặt chẽ |
| **Vector Database** | Qdrant v1.19+ | Lưu trữ vector đa chiều kết hợp Sparse BM25 index, lọc payload theo `document_id` |
| **Dense Embedding** | `intfloat/multilingual-e5-small` | Mô hình ngôn ngữ đa ngữ (384 dimensions) tối ưu biểu diễn ngữ nghĩa tiếng Việt |
| **Sparse BM25** | `fastembed` (`Qdrant/bm25`) | Đánh chỉ mục từ khóa từ vựng tiếng Việt (loại bỏ stemmer tiếng Anh) |
| **Reranker** | `cross-encoder/mmarco-mMiniLMv2-L12-H384-v1` | Chấm điểm tương quan chéo giữa câu hỏi và từng chunk ứng viên để chọn ra top kết quả phù hợp nhất |
| **Generative LLM** | Google Gemini (Gemini SDK) | Sinh câu trả lời và trích xuất chuẩn đầu ra có cấu trúc (Structured Outputs JSON) |
| **Document Parsing** | PyMuPDF (fitz), python-docx | Trích xuất văn bản có cấu trúc từ PDF giáo trình (nhận diện mục lục, banner) và tài liệu Word DOCX |
| **Báo cáo & Export** | openpyxl | Tạo và định dạng bảng Rubric đánh giá năng lực ra file Excel |
| **Security** | PyJWT, Cryptography | Xác thực ủy quyền Bearer JWT đồng bộ với Core API Backend |

---

## 3. Kiến trúc Pipeline thiết kế hệ thống

Hệ thống được thiết kế theo 2 pipeline cốt lõi:

### 3.1. Luồng Nạp và Đánh chỉ mục tài liệu (Document Ingestion Pipeline)
1. **Trích xuất (Extraction)**: Parser bóc tách văn bản số, lọc bỏ tiêu đề lặp lại (header/banner) và nhận diện cấu trúc mục lục trang.
2. **Phân đoạn văn bản (Chunking)**: Chia nhỏ văn bản theo token (cỡ 350 tokens, độ gối overlap 50 tokens) đảm bảo bao trọn vẹn ngữ cảnh một hoạt động sư phạm và không vượt trần 512 tokens khi vào Cross-Encoder.
3. **Mã hóa kép (Dual Embedding)**:
   - Tạo vector ngữ nghĩa dense với tiền tố `passage: ` qua mô hình E5 đa ngữ.
   - Tạo vector từ khóa sparse BM25 qua FastEmbed.
4. **Lưu trữ vector index (Upsert)**: Đẩy vector vào Qdrant collection `document_chunks` với payload index `document_id` (hỗ trợ lọc tìm kiếm theo tài liệu được chỉ định).

### 3.2. Luồng Truy xuất & Sinh nội dung (Retrieval & Generation Pipeline)
1. **Tìm kiếm lai (Hybrid Search)**: Nhận câu hỏi người dùng $\to$ Tạo vector truy vấn (`query: ` + sparse BM25) $\to$ Truy vấn song song Prefetch Top 30 Dense + Top 30 Sparse trong Qdrant.
2. **Hợp nhất thứ hạng (Reciprocal Rank Fusion - RRF)**: Kết hợp kết quả từ 2 nhánh truy vấn với trọng số $k=60$ để chọn ra Top 15 ứng viên tiềm năng nhất.
3. **Tái xếp hạng (Cross-Encoder Reranking)**: Đưa cặp `(Query, Candidate Chunk)` vào Cross-Encoder chấm điểm tương quan thực tế, chọn Top 4 chunk liên quan nhất.
4. **Sinh câu trả lời có căn cứ (Grounded Generation)**: Ghép Top 4 chunks thành Context có đánh số nguồn $\to$ Gửi tới Gemini LLM yêu cầu trả lời dựa trên bằng chứng và xuất kèm trích dẫn số trang chính xác.

---

## 4. Hướng dẫn cài đặt & Khởi chạy

### Yêu cầu hệ thống
- Python 3.12+
- Docker & Docker Compose (cho Qdrant Vector DB)

### Các bước cài đặt

1. **Khởi động Qdrant Vector Database**:
   ```bash
   docker compose up -d
   ```

2. **Cấu hình biến môi trường**:
   Sao chép file `.env.example` thành `.env` và cập nhật thông tin:
   ```env
   RAG_JWT_ISSUER=CmsEdu.Api
   RAG_JWT_AUDIENCE=CmsEdu.Web
   RAG_JWT_SECRET=your_jwt_secret_at_least_32_bytes_long
   RAG_GEMINI_API_KEY=your_gemini_api_key
   ```

lưu ý secret key ở module và core phải trùng nhau
3. **Cài đặt thư viện**:
   ```bash
   pip install -e .
   ```

4. **Khởi chạy API Server**:
   ```bash
   uvicorn main:app --host 0.0.0.0 --port 8000 --reload
   ```

