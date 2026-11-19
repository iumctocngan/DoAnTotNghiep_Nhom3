# Module 1 — AI Student Assessment

Project `CmsEdu.Module1.csproj` chứa API đánh giá, đọc rubric Excel, tính điểm và sinh nhận xét. Backend `CmsEdu.Api` nạp project này để chạy API. Module 1 dùng chung đăng nhập, phân quyền, SQL Server và migrations của backend. Frontend giáo viên nằm trong `frontend/src/pages/teacher`.

Mở **Đánh giá học sinh AI** tại `/assessments` bằng tài khoản Admin hoặc Teacher. Giáo viên chỉ truy cập lớp mình phụ trách.

1. Trong **Hỏi đáp học liệu**, chọn lớp, bấm **Tạo CLO & Rubric**, chọn bài học trong chương trình và tài liệu PDF. Hệ thống lưu tệp gốc và tiêu chí đã tách vào bảng `LessonClo` theo lớp và bài.
2. Trong Module 1, chọn lớp và buổi học. Nếu buổi đã gắn đúng bài, hệ thống tự chọn rubric từ CLO đã lưu. Buổi đã có điểm tiếp tục dùng phiên bản rubric đã chấm. Nhập Excel và tạo rubric thủ công vẫn dùng được khi cần. Chỉ học sinh có mặt trong buổi mới được đánh giá.
3. Rubric mới chia 70% cho CLO bài học và 10% cho mỗi mục **Thái độ học tập**, **Kỹ năng học tập**, **Điểm sách bài tập**. Chọn mức 1–5 cho các tiêu chí rubric và nhập điểm sách bài tập trực tiếp trên thang 0–10. Sau đó bấm **Lưu điểm đã chọn**.
4. Bấm **Tạo nhận xét**, chỉnh sửa bản nháp rồi bấm **Xác nhận và lưu nhận xét đã chọn**. Nhận xét mới từ Gemini có tối thiểu 250 từ, dựa vào mô tả CLO, điểm, mức rubric và lịch sử đối chiếu. Bảng xử lý tối đa ba yêu cầu cùng lúc và tái sử dụng bản nháp đã lưu.
5. Dùng **Xem báo cáo cá nhân** và chọn lượt đánh giá. Báo cáo hiển thị tên, lớp, ngày, điểm, mức đánh giá, cảnh báo nếu có, nội dung đã học, bảng đánh giá, nhận xét và phần cần luyện tập. Chọn **Xuất báo cáo PDF** để lưu bố cục này.

## Cách tính

- Điểm bài = tổng(điểm tiêu chí × trọng số) / 100, làm tròn 2 chữ số.
- Điểm trung bình: trung bình các bài của ghi danh. Tiến bộ: điểm bài cuối trừ bài đầu, theo ngày rồi ID; cần ít nhất hai bài. Đây là chỉ báo đơn giản, các bài khác độ khó có thể không so sánh trực tiếp được.
- CLO = trung bình có trọng số các tiêu chí cùng mã CLO qua lịch sử × 10 (%). Từ 80% là điểm mạnh, dưới 50% cần hỗ trợ.
- Chuyên cần: số lần có mặt / số bản ghi điểm danh của các buổi đã hoàn thành. Chưa điểm danh không được tự xem là vắng.
- Cảnh báo cao: điểm trung bình < 5 hoặc chuyên cần < 70%; trung bình: điểm < 6,5 hoặc giảm hơn 1 điểm; chưa có điểm: chưa đủ dữ liệu. Cảnh báo dùng quy tắc, không phải mô hình dự đoán.

## Khởi chạy

Backend dùng `DbInitializer` để áp dụng migration khi khởi động trong Development. Migration `20261120010000_AddLessonClo` lưu đầu ra Module 2 và liên kết rubric mới với phiên bản CLO của bài. Cần SQL Server hoạt động và tài khoản có quyền migration.

Model mặc định là `gemini-3.5-flash-lite`. Để lưu API key một lần trên máy phát triển, chạy từ thư mục gốc dự án:

```powershell
$geminiKeys = Read-Host 'Dán Gemini API key, cách nhau bằng dấu phẩy nếu có nhiều key' -AsSecureString
@{ 'AssessmentAI:ApiKey' = [System.Net.NetworkCredential]::new('', $geminiKeys).Password } |
    ConvertTo-Json -Compress | dotnet user-secrets set --project backend/src/CmsEdu.Api
Remove-Variable geminiKeys
dotnet run --project backend/src/CmsEdu.Api
```

ASP.NET Core tự đọc User Secrets khi chạy ở môi trường Development. Nếu máy đã cấp biến môi trường `GEMINI_API_KEY`, Module 1 dùng biến này với endpoint Gemini mặc định khi chưa có `AssessmentAI:ApiKey`. Môi trường triển khai có thể cấp `AssessmentAI__ApiKey` qua kho bí mật hoặc biến môi trường. `AssessmentAI__Model` từ cấu hình triển khai sẽ ghi đè model mặc định. Khi cần tạo bản nháp, yêu cầu gửi dữ liệu rubric, điểm và nhận xét lịch sử phục vụ đối chiếu. Thiếu cấu hình trả 503, vượt hạn mức trả 429 và các lỗi khác từ dịch vụ AI trả 502.

`AssessmentAI:ApiKey` nhận một key hoặc danh sách key cách nhau bằng dấu phẩy. Module 1 gọi API `generateContent` của Gemini theo mặc định. Hệ thống thử theo thứ tự đã lưu và chuyển sang key tiếp theo khi dịch vụ báo key không hợp lệ, từ chối truy cập hoặc vượt hạn mức. Các key chung một Google Cloud project có thể dùng chung hạn mức.

## API tích hợp

- `GET /api/assessments/classes`
- `GET /api/assessments/classes/{id}/students`
- `GET /api/assessments/classes/{id}/rubrics`
- `GET /api/assessments/classes/{id}/sessions`
- `GET /api/assessments/sessions/{id}/matrix?rubricId={rubricId}` trả danh sách lớp, điểm danh và điểm đã lưu.
- `POST /api/assessments/sessions/{id}/rubrics/{rubricId}/grades` nhận `{ rows: [{ enrollmentId, scores }] }` và lưu cả lượt chấm trong một lần.
- `POST /api/assessments/rubrics`: `{ classId, name, criteria: [{ name, weight, clo, description }] }`. Các tiêu chí có thể kèm `mucDanhGia` chứa mô tả năm mức.
- `POST /api/assessments/classes/{id}/rubrics/import`: nhận file Excel do Module 2 xuất qua trường multipart `tep`, trả về tiêu chí xem trước.
- `POST /api/assessments`: `{ enrollmentId, rubricId, title, date: "2026-10-18", scores: [8, 7] }`. Thứ tự điểm trùng thứ tự tiêu chí.
- `GET /api/assessments/enrollments/{id}/report`: báo cáo học tập, CLO, Rubric, gợi ý ôn, cảnh báo và nhận xét kèm trạng thái duyệt.
- `POST /api/assessments/{id}/feedback`
- `POST /api/assessments/{id}/approve`: `{ feedback }`

Các API đánh giá giới hạn Admin/Teacher. Nội dung nhận xét, điểm, thời điểm duyệt và người duyệt được lưu trong bảng `Assessment` của SQL Server. Module 3 đọc nhận xét đã duyệt gần nhất của ghi danh hiện tại từ cùng cơ sở dữ liệu. Nếu chưa có, chatbot dùng nhận xét cũ từ `StudentRemarks` và để trống điểm.

## Kiểm tra

`npm run build` trong frontend; `dotnet build backend/src/CmsEdu.Api -m:1`; `dotnet test backend/tests/CmsEdu.UnitTests -m:1`.
