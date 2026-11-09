# Hướng dẫn chạy Module 3

Module 3 gồm AI Service viết bằng FastAPI, phục vụ dự đoán tái phí bằng LightGBM/SHAP và truy xuất ngữ cảnh, sinh kịch bản CSKH bằng LLM. Frontend gọi các chức năng này qua Core API .NET; khóa Gemini/OpenAI chỉ được đọc bởi AI Service từ `module3/.env`.

> Hướng dẫn này dành cho môi trường phát triển Windows/PowerShell. Core API mặc định chuyển tiếp đến AI Service tại `http://127.0.0.1:8001/`. Cần chạy SQL Server, Core API, AI Service và frontend để dùng luồng giao diện đầy đủ.

## 1. Yêu cầu

- Python 3.11 trở lên.
- SQL Server có database `CmsEduDb`, đã chạy backend/migrations và có dữ liệu phù hợp.
- Microsoft ODBC Driver 17 for SQL Server.
- Khóa API Gemini hoặc OpenAI nếu muốn gọi chức năng sinh kịch bản.
- .NET 10 SDK và Node.js/npm để chạy Core API và frontend.
- Git Bash không bắt buộc; các lệnh bên dưới sử dụng PowerShell.

## 2. Cài đặt môi trường Python

Mở PowerShell tại thư mục gốc repository rồi chạy:

```powershell
Set-Location .\module3
py -3.11 -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -r .\requirements.txt
```

Nếu PowerShell chặn kích hoạt virtual environment, chỉ cho phép script trong cửa sổ hiện tại rồi kích hoạt lại:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\.venv\Scripts\Activate.ps1
```

## 3. Cấu hình `.env`

Trong thư mục `module3`, tạo `.env` từ file mẫu nếu chưa có:

```powershell
if (-not (Test-Path .\.env)) { Copy-Item .\.env.example .\.env }
notepad .\.env
```

Kiểm tra và điều chỉnh các biến sau trong `.env` cho đúng máy đang chạy. Không commit `.env` hoặc chia sẻ khóa API:

```dotenv
DB_CONNECTION_STRING=DRIVER={ODBC Driver 17 for SQL Server};SERVER=.\SQLEXPRESS;DATABASE=CmsEduDb;Trusted_Connection=Yes;TrustServerCertificate=Yes;
GEMINI_API_KEY=
OPENAI_API_KEY=
LLM_PROVIDER=gemini
LLM_MODEL=gemini-3.5-flash-lite
ML_MODEL_PATH=./ai_service/ml_models/renewal_model.joblib
ML_MODEL_VERSION=v1.0.0-lgbm
DEBUG=true
LOG_LEVEL=INFO
```

- Chỉnh `SERVER`, `DATABASE` và phương thức xác thực SQL Server theo cấu hình máy.
- Đặt khóa cho provider được chọn ở `LLM_PROVIDER` (`gemini` hoặc `openai`). Chức năng sinh kịch bản sẽ trả lỗi cấu hình nếu khóa tương ứng đang để trống.
- `ML_MODEL_PATH` mặc định được tính tương đối từ thư mục `module3`; giữ nguyên thư mục làm việc như các lệnh trong hướng dẫn.

## 4. Chạy AI Service

Mở PowerShell tại thư mục gốc repository rồi chạy:

```powershell
cd module3
$env:DEBUG = "true"
$env:PYTHONUTF8 = "1"
.\.venv\Scripts\python.exe -m uvicorn ai_service.main:app --app-dir . --host 127.0.0.1 --port 8001
```

Lệnh chuyển working directory vào `module3`, nên Python tìm được package `ai_service`, đồng thời cấu hình `.env` và đường dẫn model được đọc đúng vị trí. Kích hoạt virtual environment không bắt buộc vì lệnh gọi trực tiếp Python bên trong `.venv`.

Khi chạy thành công, mở:

- Health check: <http://127.0.0.1:8001/health>
- Swagger UI: <http://127.0.0.1:8001/docs>

Hoặc kiểm tra health check từ một PowerShell khác:

```powershell
Invoke-RestMethod http://127.0.0.1:8001/health
```

`/health` chỉ xác nhận tiến trình FastAPI đang chạy; endpoint này không kiểm tra SQL Server, model hoặc nhà cung cấp LLM.

Core API không nhận hoặc lưu khóa Gemini/OpenAI. Chỉ cấu hình khóa mới trong `module3/.env`, sau đó khởi động lại AI Service.

## 5. Các endpoint AI hiện được khai báo

Các endpoint dưới đây gọi trực tiếp AI Service tại `http://127.0.0.1:8001`. Chúng không phải endpoint .NET có JWT/RBAC; chỉ sử dụng trên máy phát triển đáng tin cậy, không mở trực tiếp ra mạng công cộng.

| Method | Path | Phụ thuộc |
|---|---|---|
| `GET` | `/health` | Chỉ cần AI Service đang chạy |
| `POST` | `/api/ml/predict-student` | SQL Server, enrollment hợp lệ, model |
| `POST` | `/api/ml/predict-batch` | SQL Server, dữ liệu enrollment, model |
| `GET` | `/api/ai/cskh/student-context/{student_id}` | SQL Server và dữ liệu học viên |
| `POST` | `/api/ai/cskh/call-script` | SQL Server, dữ liệu context và khóa LLM |

Ví dụ dự đoán cho một học viên (thay ID bằng học viên và enrollment có thật trong database):

```powershell
$body = @{
    student_id = 105
    enrollment_id = 42
} | ConvertTo-Json

Invoke-RestMethod `
    -Method Post `
    -Uri http://127.0.0.1:8001/api/ml/predict-student `
    -ContentType "application/json" `
    -Body $body
```

Ví dụ truy xuất context và sinh kịch bản:

```powershell
Invoke-RestMethod http://127.0.0.1:8001/api/ai/cskh/student-context/105

$body = @{
    student_id = 105
    question = "Hãy tạo kịch bản tư vấn dựa trên dữ liệu học viên hiện có."
} | ConvertTo-Json

Invoke-RestMethod `
    -Method Post `
    -Uri http://127.0.0.1:8001/api/ai/cskh/call-script `
    -ContentType "application/json" `
    -Body $body
```

Prediction router lưu snapshot dự đoán mới vào `StudentRenewalPredictions`; vì vậy endpoint dự đoán cần quyền ghi database cho bảng này. Việc đọc context sử dụng SQL Server và trả `404` khi không tìm thấy học viên.

## 6. Model LightGBM

Nếu chưa có file `ai_service/ml_models/renewal_model.joblib`, lần inference đầu tiên sẽ gọi hàm huấn luyện tích hợp trong `ai_service/train_model.py` để tạo model từ dữ liệu mô phỏng. Có thể chủ động tạo model theo cách đó:

```powershell
python .\ai_service\train_model.py
```

Pipeline `ai_service/scripts/prepare_dataset_and_train.py` là lựa chọn khác: nó lấy feature từ SQL Server, đọc nhãn tại `Tài liệu/labeled_student_targets.json` và tạo artifact model. Chỉ chạy khi đã kiểm tra database, dữ liệu nhãn và hiểu rõ pipeline huấn luyện; pipeline này có thể ghi đè model/metadata hiện có:

```powershell
python .\ai_service\scripts\prepare_dataset_and_train.py
```

Không dùng kết quả huấn luyện từ dữ liệu mô phỏng thay cho đánh giá model trên dữ liệu triển khai thực tế.

## 7. Chạy kiểm thử

Khi virtual environment đang kích hoạt, chạy từ thư mục `module3`:

```powershell
python -m pytest .\tests -q
```

Chỉ chạy một nhóm test:

```powershell
python -m pytest .\tests\test_ml_engine.py -q
python -m pytest .\tests\test_cskh_context.py -q
python -m pytest .\tests\test_feature_store.py -q
python -m pytest .\tests\test_prediction.py -q
```

## 8. Chạy cùng Backend và Frontend

Đảm bảo SQL Server đã chạy. Trong môi trường Development, Core API tự áp dụng migration khi khởi động. Mặc định Core API kết nối AI Service ở cổng `8001`; nếu chạy AI Service ở địa chỉ khác, đặt biến môi trường `AiService__BaseUrl` cho tiến trình backend.

Mở các cửa sổ PowerShell riêng tại thư mục gốc repository:

```powershell
dotnet run --project .\backend\src\CmsEdu.Api
```

Trong một PowerShell khác, chạy AI Service theo mục 4 (working directory là `module3`).

```powershell
Set-Location .\frontend
npm install
npm run dev
```

Frontend Vite proxy `/api` về Core API .NET ở cổng `5151`. Gateway Module 3 giới hạn các API dự đoán/CSKH cho Admin và CustomerCare; không gọi Gemini trực tiếp từ trình duyệt.

## 9. Xử lý lỗi thường gặp

| Hiện tượng | Hướng kiểm tra |
|---|---|
| `ModuleNotFoundError: No module named 'ai_service'` | Chạy lệnh ở mục 4 từ thư mục gốc repository. Nếu dùng terminal khác, chạy `Set-Location .\module3` trước `.\.venv\Scripts\python.exe -m uvicorn ai_service.main:app --app-dir . --reload --host 127.0.0.1 --port 8001`. |
| `pyodbc` không tìm thấy driver | Cài Microsoft ODBC Driver 17 for SQL Server; kiểm tra chính tả tên driver trong `DB_CONNECTION_STRING`. |
| Không kết nối SQL Server | Kiểm tra SQL Server/instance đang chạy, tên `SERVER`, database, quyền tài khoản và firewall. |
| Thiếu khóa Gemini/OpenAI | Đặt khóa tương ứng trong `.env`; kiểm tra `LLM_PROVIDER` và `LLM_MODEL`. Khởi động lại Uvicorn sau khi sửa cấu hình. |
| Lỗi không tìm thấy học viên hoặc enrollment | Dùng ID có thật trong database; xem chi tiết lỗi tại Swagger/terminal đang chạy Uvicorn. |
| Core API báo AI Service tạm thời không khả dụng | Kiểm tra AI Service có chạy tại `AiService:BaseUrl` (mặc định `http://127.0.0.1:8001/`) và xem lỗi ở terminal Uvicorn. |
| Giao diện báo chưa cấu hình Gemini/OpenAI | Đặt khóa provider tương ứng trong `module3/.env`, khởi động lại Uvicorn; không gửi khóa vào frontend hoặc commit file `.env`. |
| Không xem/lưu được phiên chat | Đăng nhập bằng vai trò Admin hoặc CustomerCare; phiên chat chỉ được truy cập bởi nhân viên đã tạo phiên. |

## 10. Dừng dịch vụ

Ở cửa sổ PowerShell đang chạy Uvicorn hoặc ứng dụng khác, nhấn `Ctrl+C`. Khi hoàn tất, thoát virtual environment:

```powershell
deactivate
```
