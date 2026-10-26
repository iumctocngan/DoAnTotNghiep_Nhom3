module 2 này là một module RAG của hệ thống quản lý trung tâm giáo dục

- nhận jwt và xác thực (jwt có claim role -> chỉ có admin , teacher được truy cập endpoint)
- techstack : fastapi + python + Qdrant + docker để host db + venv + gemini-apikey free (model 3.5 flash)
- chunk token aware (350 token overlap 50)
- embedding local (intfloat/multilingual-e5-small)
- reranking local (cross-encoder/mmarco-mMiniLMv2-L12-H384-v1)
- model lần đầu kết nối huggingface nếu chưa có thì tải về, lần sau 100% local không gọi lên server nữa

giáo viên tạo notebook sau đó upload document (giống notebookLM của google)
Mỗi Notebook chỉ có duy nhất một chat session, lưu cuộc hội thoại, nhưng sẽ không dùng làm context
Chỉ giáo viên tạo Notebook mới được xem

đầu vào (document):
- file pdf, docx
- pdf native text , 0 có TOC/Bookmark, tự gắn section, logic để gắn cũng khá đơn giản do heading thường là in hoa chữ to ta có thể gắn làm section luôn
- giới hạn đầu vào <=25 MB, không nhận pdf scan (tài liệu của trung tâm không có pdf scan)
- do là sách trẻ em nên có rất nhiều ảnh (file pdf)

**Lưu ý** : Phần xử lý tài liệu document (file pdf) là phần khó, quan trọng nhất trong pipeline của toàn bộ hệ thống file docx đơn giản hơn chỉ có text. Tài liệu ở folder data

mục đích của module-rag: 
- người dùng tra cứu, đặt câu hỏi bằng Tiếng Việt hệ thống trả về đáp án có trích dẫn nguồn
- Generate file xlsx CLO/Rubic gồm 6 cột cố định số CLO, Tiêu chí đánh giá, Chưa đạt(0-4.9), Cần cải thiện(5-6.9), Đạt(7-8.4), Xuất sắc(8.5-10). Chỉ generate 0 cần quản lý.

pipeline cơ bản : 
ingest: pdf ->load->chunking->local embedding->vector db
retrieval: hybrid search with fusion + reranking

evaluate
- evaluate retrieval: hit@k, mrr, recall@k so sánh để thấy tác dụng của advanced đo ở 4 mức vector search | hybrid search with fusion | full (+rerank)
- đánh giá generate câu trả lời: độ ảo giác và bám nguồn
- dùng matplot để trực quan

---

### Thiết kế Cơ sở dữ liệu Tối giản cho Core (.NET + SQL Server)
Phần này định nghĩa cấu trúc 2 bảng quan hệ chuẩn mực để triển khai phía Backend Core (.NET Clean Architecture), đồng bộ với Module 2 RAG (Qdrant Vector Engine):

#### 1. Bảng `Notebooks` (Không gian học liệu của giáo viên)
```sql
CREATE TABLE Notebooks (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Title NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000) NULL,
    TeacherId UNIQUEIDENTIFIER NOT NULL, -- Chỉ giáo viên sở hữu mới được xem/truy vấn
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

#### 2. Bảng `Documents` (Tài liệu PDF/DOCX trong Notebook)
```sql
CREATE TABLE Documents (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    NotebookId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES Notebooks(Id) ON DELETE CASCADE,
    FileName NVARCHAR(255) NOT NULL,
    StoragePath NVARCHAR(500) NOT NULL,     -- Đường dẫn file lưu trữ trên server Core
    FileSize BIGINT NOT NULL,              -- Giới hạn <= 25MB
    DocumentType VARCHAR(10) NOT NULL,      -- 'pdf' hoặc 'docx'
    Status VARCHAR(20) NOT NULL DEFAULT 'processing', -- 'processing', 'ready', 'failed'
    ErrorMessage NVARCHAR(500) NULL,       -- Ghi nhận lỗi nếu Ingestion thất bại
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

#### 3. Cơ chế đồng bộ giữa Core (.NET) và Module 2 RAG (Qdrant)
- **Nạp tài liệu**: Core tạo record `Documents` với trạng thái `processing` $\rightarrow$ gọi API RAG `POST /api/v1/notebooks/{notebook_id}/documents/ingest` $\rightarrow$ Nhận kết quả thành công thì Core đổi trạng thái sang `ready`.
- **Xóa 1 tài liệu**: Core gọi API RAG `DELETE /api/v1/notebooks/{notebook_id}/documents/{document_id}` để xóa toàn bộ vector chunks mang tag `document_id` trong Qdrant $\rightarrow$ Sau đó Core xóa record trong SQL Server.
- **Xóa toàn bộ Notebook**: Core gọi API RAG `DELETE /api/v1/notebooks/{notebook_id}` để xóa sạch vector theo tenant tag `notebook_id` trong Qdrant $\rightarrow$ Xóa record `Notebooks` trong SQL Server (tự động xóa CASCADE các documents liên quan).
- **Hỏi đáp RAG**: Core gọi `POST /api/v1/notebooks/{notebook_id}/chat` (kèm danh sách lọc `document_ids` nếu giáo viên tick chọn trên UI).

