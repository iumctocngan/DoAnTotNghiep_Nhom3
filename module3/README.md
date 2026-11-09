# Module 3: AI Finance & Customer Care — Python AI Service

Dịch vụ Python FastAPI phục vụ hai nhiệm vụ chính:
1. **ML Renewal Engine** (Tuần 2): Dự đoán xác suất tái phí học viên bằng LightGBM + SHAP.
2. **LLM CSKH Chatbot** (Tuần 3): Chatbot hỗ trợ nhân viên CSKH tra cứu hồ sơ học sinh và sinh kịch bản cuộc gọi.

## Cấu trúc thư mục

```
module3/
├── ai_service/           # FastAPI application
│   ├── main.py           # Entry point
│   ├── config.py         # Settings từ biến môi trường
│   ├── database.py       # Kết nối SQL Server
│   ├── models/           # SQLAlchemy models (read-only từ Shared DB)
│   ├── schemas/          # Pydantic schemas
│   ├── routers/
│   │   ├── prediction.py # /api/ml/predict-batch, predict-student
│   │   └── cskh.py       # /api/ai/cskh/student-context
│   ├── services/
│   │   ├── feature_store.py    # Trích xuất 12 features từ SQL Server
│   │   ├── ml_engine.py        # LightGBM inference + SHAP
│   │   └── context_retriever.py # Gom dữ liệu cho Chatbot
│   └── ml_models/        # File .joblib model đã train (gitignored)
├── notebooks/            # Jupyter notebooks huấn luyện model (Tuần 2)
├── tests/                # Pytest unit tests
├── requirements.txt
├── .env.example
└── README.md
```

## Yêu cầu

- Python 3.11+
- SQL Server (kết nối chung với backend .NET)

## Khởi động

```bash
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
cp .env.example .env   # Điền connection string SQL Server
uvicorn ai_service.main:app --reload --port 8001
```

## Endpoints (Tuần 1 — Scaffold)

| Method | Path | Mô tả |
|--------|------|-------|
| `GET`  | `/health` | Health check |
| `POST` | `/api/ml/predict-student` | On-demand dự đoán 1 học sinh (**Tuần 2**) |
| `POST` | `/api/ml/predict-batch`   | Batch dự đoán toàn hệ thống (**Tuần 2**) |
| `GET`  | `/api/ai/cskh/student-context/{student_id}` | Gom dữ liệu context cho Chatbot (**Tuần 3**) |
