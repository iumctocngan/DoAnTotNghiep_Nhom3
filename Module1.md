# Module 1 — AI Student Assessment

Mở **Đánh giá học sinh AI** tại `/assessments` bằng tài khoản Admin hoặc Teacher. Giáo viên chỉ truy cập lớp mình phụ trách.

1. Chọn lớp, tạo Rubric: tên tiêu chí, mã CLO, nội dung/năng lực cần ôn và trọng số. Tổng trọng số phải bằng 100%. Rubric được lưu bất biến để giữ đúng lịch sử điểm.
2. Chọn học viên theo ghi danh, chọn Rubric, nhập bài đánh giá, ngày và điểm từng tiêu chí (0–10).
3. Xem báo cáo học tập, lịch sử, báo cáo CLO và Rubric; xuất JSON.
4. Sinh nhận xét AI, chỉnh sửa rồi duyệt; hoặc giáo viên tự nhập nhận xét khi chưa cấu hình AI. Nhận xét đã duyệt không được ghi đè.

## Cách tính

- Điểm bài = tổng(điểm tiêu chí × trọng số) / 100, làm tròn 2 chữ số.
- Điểm trung bình: trung bình các bài của ghi danh. Tiến bộ: điểm bài cuối trừ bài đầu, theo ngày rồi ID; cần ít nhất hai bài. Đây là chỉ báo đơn giản, các bài khác độ khó có thể không so sánh trực tiếp được.
- CLO = trung bình có trọng số các tiêu chí cùng mã CLO qua lịch sử × 10 (%). Từ 80% là điểm mạnh, dưới 50% cần hỗ trợ.
- Chuyên cần: số lần có mặt / số bản ghi điểm danh của các buổi đã hoàn thành. Chưa điểm danh không được tự xem là vắng.
- Cảnh báo cao: điểm trung bình < 5 hoặc chuyên cần < 70%; trung bình: điểm < 6,5 hoặc giảm hơn 1 điểm; chưa có điểm: chưa đủ dữ liệu. Cảnh báo dùng quy tắc, không phải mô hình dự đoán.

## Khởi chạy

Backend hiện có `DbInitializer` áp dụng migration khi khởi động. Migration `20261018090000_StudentAssessment` thêm hai bảng, không thay đổi dữ liệu cũ. Cần SQL Server hoạt động và tài khoản có quyền migration. Chạy backend/frontend theo README.

Cấu hình qua biến môi trường (không ghi API key vào Git):

```powershell
$env:AssessmentAI__ApiKey = '<API key>'
$env:AssessmentAI__Model = '<model hỗ trợ chat completions>'
# Tùy chọn: dịch vụ tương thích Chat Completions
$env:AssessmentAI__Endpoint = 'https://api.openai.com/v1/chat/completions'
dotnet run --project backend/src/CmsEdu.Api
```

Chỉ khi bấm sinh nhận xét, dữ liệu báo cáo học tập của học viên được gửi tới endpoint đã cấu hình. Thiếu cấu hình trả 503; lỗi dịch vụ trả 502 và không lưu nhận xét giả.

## API tích hợp

- `GET /api/assessments/classes`
- `GET /api/assessments/classes/{id}/students`
- `GET /api/assessments/classes/{id}/rubrics`
- `POST /api/assessments/rubrics`: `{ classId, name, criteria: [{ name, weight, clo, description }] }`. Module 2 có thể cấp định nghĩa này, giáo viên kiểm tra và lưu qua API. Chưa có kết nối tự động tới Module 2.
- `POST /api/assessments`: `{ enrollmentId, rubricId, title, date: "2026-10-18", scores: [8, 7] }`. Thứ tự điểm trùng thứ tự tiêu chí.
- `GET /api/assessments/enrollments/{id}/report`: báo cáo học tập, CLO, Rubric, gợi ý ôn, cảnh báo và nhận xét kèm trạng thái duyệt.
- `POST /api/assessments/{id}/feedback`
- `POST /api/assessments/{id}/approve`: `{ feedback }`

Các API hiện giới hạn Admin/Teacher. Module 3 chưa có quyền truy cập riêng; khi tích hợp cần hợp đồng và phân quyền đọc báo cáo đã duyệt, không lấy bản nháp làm nhận xét chính thức.

## Kiểm tra

`npm run build` trong frontend; `dotnet build backend/src/CmsEdu.Api -m:1`; `dotnet test backend/tests/CmsEdu.UnitTests -m:1`.

Đã xác minh: backend và frontend build thành công; 77/77 unit test đạt, gồm 7 test mới cho công thức điểm và migration snapshot. Test host cần chạy ngoài sandbox trong môi trường hiện tại. Chưa chạy thử migration trên SQL Server và chưa gọi LLM thực tế. Build còn cảnh báo NuGet có sẵn của `System.Security.Cryptography.Pkcs` 9.0.11 (NU1902/NU1903).
