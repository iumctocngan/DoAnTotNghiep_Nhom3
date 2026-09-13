# BÁO CÁO NGHIỆP VỤ ĐIỂM DANH (ATTENDANCE) VÀ KẾT QUẢ TRIỂN KHAI

## 1. Tổng quan các nội dung đã hoàn thành

Tuân thủ nghiêm ngặt các tài liệu thiết kế hệ thống (`docs/core-database-design.md`, `docs/core-api-design.md`, `docs/overview.md`), phân hệ quản lý điểm danh đã được hoàn thành đầy đủ với các thành phần:

1. **DTOs Điểm danh** ([`DuLieuDiemDanh.cs`](file:///c:/Users/ADMIN/OneDrive/Dokumen/Máy%20tính/ĐATN/backend/src/CmsEdu.Application/Attendances/DuLieuDiemDanh.cs)):
   - `YeuCauLuuDiemDanhChiTiet`: Dữ liệu điểm danh của từng học viên (`EnrollmentId`, `Status` (Present/Absent), `Note`).
   - `YeuCauLuuDiemDanhBuoiHoc`: DTO danh sách gửi lên để lưu điểm danh cả lớp theo lô.
   - `PhanHoiDiemDanhHocVien`: Thông tin học viên kèm trạng thái điểm danh chi tiết, người đánh dấu, thời gian cập nhật.
   - `PhanHoiDiemDanhBuoiHoc`: Tổng hợp thông tin buổi học, số lượng có mặt, vắng mặt, chưa điểm danh và danh sách chi tiết.

2. **Giao diện dịch vụ** ([`IDichVuDiemDanh.cs`](file:///c:/Users/ADMIN/OneDrive/Dokumen/Máy%20tính/ĐATN/backend/src/CmsEdu.Application/Common/Interfaces/IDichVuDiemDanh.cs)):
   - `LayDanhSachDiemDanhTheoBuoiHocAsync`: Truy xuất danh sách điểm danh cho buổi học.
   - `LuuDiemDanhTheoBuoiHocAsync`: Lưu điểm danh đồng thời cho toàn bộ học viên hợp lệ.

3. **Cài đặt dịch vụ** ([`DichVuDiemDanh.cs`](file:///c:/Users/ADMIN/OneDrive/Dokumen/Máy%20tính/ĐATN/backend/src/CmsEdu.Infrastructure/Services/DichVuDiemDanh.cs)):
   - Xác thực phân quyền: Chỉ cho phép **Admin** hoặc **Giáo viên chính phụ trách lớp**.
   - Kiểm tra trạng thái buổi học: Từ chối nếu buổi học đã hủy (`Cancelled`) hoặc đã hoàn tất (`Completed`).
   - Lọc chính xác danh sách ghi danh (`Enrollment`) hợp lệ tại ngày diễn ra buổi học.
   - Kiểm tra tính đầy đủ, không trùng lặp và không chứa học viên ngoài danh sách hợp lệ.
   - Thực thi lưu trong **Transaction cơ sở dữ liệu**.

4. **Đăng ký Dependency Injection** ([`DependencyInjection.cs`](file:///c:/Users/ADMIN/OneDrive/Dokumen/Máy%20tính/ĐATN/backend/src/CmsEdu.Infrastructure/DependencyInjection.cs)):
   - Đăng ký `IDichVuDiemDanh` vào DI container với vòng đời `Scoped`.

5. **Bộ điều khiển API** ([`BuoiHocController.cs`](file:///c:/Users/ADMIN/OneDrive/Dokumen/Máy%20tính/ĐATN/backend/src/CmsEdu.Api/Controllers/BuoiHocController.cs)):
   - `GET /api/sessions/{id}/attendance`: Xem danh sách điểm danh buổi học.
   - `PUT /api/sessions/{id}/attendance`: Lưu điểm danh cả lớp theo lô.

6. **Kiểm thử đơn vị** ([`KiemThuDichVuDiemDanh.cs`](file:///c:/Users/ADMIN/OneDrive/Dokumen/Máy%20tính/ĐATN/backend/tests/CmsEdu.UnitTests/KiemThuDichVuDiemDanh.cs)):
   - 10 kịch bản kiểm thử độc lập bao phủ toàn bộ các luồng thành công và lỗi ngoại lệ, 100% vượt qua.

---

## 2. Bản chất nghiệp vụ Điểm danh và Mối quan hệ với Session, Student

### 2.1. Sơ đồ thực thể và dòng chảy dữ liệu

```mermaid
erDiagram
    Student ||--o{ Enrollment : "tham gia (1 - N)"
    Class ||--o{ Enrollment : "chứa các (1 - N)"
    Class ||--o{ Session : "tổ chức (1 - N)"
    Session ||--o{ Attendance : "ghi nhận điểm danh (1 - N)"
    Enrollment ||--o{ Attendance : "lịch sử có/vắng (1 - N)"

    Student {
        int Id PK
        string StudentCode
        string FullName
        DateOnly DateOfBirth
    }

    Class {
        int Id PK
        string ClassCode
        string Name
        string MainTeacherUserId
        DateOnly StartDate
        DateOnly EndDate
    }

    Enrollment {
        int Id PK
        int StudentId FK
        int ClassId FK
        DateOnly StartDate
        DateOnly EndDate
        EnrollmentStatus Status "Active / Paused / Completed / Withdrawn"
    }

    Session {
        int Id PK
        int ClassId FK
        DateOnly SessionDate
        TimeOnly StartTime
        TimeOnly EndTime
        SessionStatus Status "Scheduled / Completed / Cancelled"
    }

    Attendance {
        int Id PK
        int SessionId FK
        int EnrollmentId FK
        AttendanceStatus Status "Present / Absent"
        string Note
        string MarkedBy
        datetime MarkedAt
        string UpdatedBy
        datetime UpdatedAt
    }
```

### 2.2. Vì sao Attendance liên kết qua Enrollment thay vì liên kết trực tiếp với Student?

Trong mô hình quản lý đào tạo trung tâm, học sinh (`Student`) không trực tiếp gắn liền với một buổi học tự do mà luôn học tập thông qua **hồ sơ ghi danh (`Enrollment`)** vào một lớp cụ thể:

1. **Bảo toàn ngữ cảnh học vụ theo thời gian:**
   - Một học sinh có thể học nhiều lớp ở các thời điểm khác nhau. `Enrollment` xác định học sinh đó đang theo học lớp nào (`ClassId`), từ ngày nào (`StartDate`) đến ngày nào (`EndDate`).
   - Ghi nhận điểm danh theo `EnrollmentId` giúp truy vết chính xác học sinh tham gia buổi học với tư cách là học viên chính thức của lớp học đó.

2. **Kiểm soát trạng thái học vụ tức thời:**
   - Học sinh có thể đang `Active`, xin `Paused` (bảo lưu), `Withdrawn` (thôi học) hoặc `Completed` (hoàn thành khóa).
   - Điểm danh thông qua `Enrollment` đảm bảo hệ thống có thể đối chiếu trạng thái ngay tại ngày diễn ra buổi học (`SessionDate`): Chỉ học viên có `Enrollment.Status == Active` và nằm trong khoảng ngày hiệu lực mới được phép điểm danh.

3. **Ràng buộc toàn vẹn dữ liệu hai chiều:**
   - Một buổi học (`Session`) thuộc về một lớp (`ClassId`).
   - Một ghi danh (`Enrollment`) cũng thuộc về một lớp (`ClassId`).
   - Điểm danh (`Attendance`) là giao điểm xác thực rằng: Học viên đó đang ghi danh hợp lệ tại đúng lớp học mà buổi học đang diễn ra.

### 2.3. Các quy tắc nghiệp vụ cốt lõi

| Quy tắc | Nội dung chi tiết |
| :--- | :--- |
| **Ràng buộc duy nhất** | Unique `(SessionId, EnrollmentId)`: Mỗi học sinh trong một buổi học chỉ có duy nhất 1 bản ghi điểm danh. |
| **Điều kiện học sinh hợp lệ** | Học sinh được điểm danh tại `SessionDate` khi và chỉ khi: Enrollment cùng `ClassId`, `Status = Active`, `StartDate <= SessionDate` và (`EndDate IS NULL` hoặc `EndDate >= SessionDate`). |
| **Loại trừ ghi danh không hợp lệ** | Các Enrollment ở trạng thái `Paused`, `Completed`, `Withdrawn` hoặc chưa đến ngày bắt đầu tuyệt đối không nằm trong danh sách điểm danh. |
| **Lưu theo lô trong Transaction** | Khi gửi yêu cầu lưu điểm danh, client phải gửi danh sách bao phủ đầy đủ 100% học viên hợp lệ. Toàn bộ thao tác thêm mới/cập nhật được thực thi an toàn trong 1 Transaction. |
| **Phân quyền điểm danh** | Chỉ **Admin** hoặc **Giáo viên chính của lớp** (`Classes.MainTeacherUserId == CurrentUser.UserId`) mới có quyền xem và lưu điểm danh. |
| **Khóa dữ liệu sau khi hoàn thành** | Buổi học `Completed` sẽ khóa toàn bộ dữ liệu điểm danh (không cho sửa, không cho xóa). Không thể thực hiện điểm danh cho buổi học đã hủy (`Cancelled`). |
| **Điều kiện để hoàn thành buổi học** | Một `Session` chỉ được chuyển sang `Completed` khi tất cả các học sinh hợp lệ tại `SessionDate` đều đã có bản ghi điểm danh đầy đủ. |

---

## 3. Kết quả thực thi kiểm thử tự động (Unit Tests)

Bộ kiểm thử đơn vị tại [`KiemThuDichVuDiemDanh.cs`](file:///c:/Users/ADMIN/OneDrive/Dokumen/Máy%20tính/ĐATN/backend/tests/CmsEdu.UnitTests/KiemThuDichVuDiemDanh.cs) đã kiểm chứng toàn bộ các quy tắc nghiệp vụ:

| STT | Tên ca kiểm thử | Kết quả | Ý nghĩa kiểm chứng |
| :---: | :--- | :---: | :--- |
| 1 | `LayDanhSachDiemDanhTheoBuoiHocAsync_TraVeDanhSachHocVienHopLeChuaDiemDanh` | **PASSED** | Lấy danh sách ban đầu khi chưa điểm danh, trả về đúng danh sách học viên hợp lệ với trạng thái null. |
| 2 | `LuuDiemDanhTheoBuoiHocAsync_LuuThanhCongTatCaHocVienHopLe` | **PASSED** | Lưu điểm danh theo lô thành công (Present/Absent, ghi chú, người tạo `MarkedBy`). |
| 3 | `LuuDiemDanhTheoBuoiHocAsync_CapNhatThanhCongDiemDanhDaTonTai` | **PASSED** | Cho phép sửa điểm danh khi buổi học chưa hoàn thành, cập nhật đúng `UpdatedBy` và `UpdatedAt`. |
| 4 | `LuuDiemDanhTheoBuoiHocAsync_ChoPhepGiaoVienChinhPhuTrachLop` | **PASSED** | Cho phép giáo viên chính của lớp thực hiện điểm danh. |
| 5 | `LuuDiemDanhTheoBuoiHocAsync_TuChoiKhiGiaoVienKhongPhuTrachLop` | **PASSED** | Ngăn chặn giáo viên khác không phụ trách lớp can thiệp điểm danh (`ForbiddenAccessException`). |
| 6 | `LuuDiemDanhTheoBuoiHocAsync_TuChoiKhiBuoiHocDaHoanThanh` | **PASSED** | Ngăn chặn sửa điểm danh khi buổi học đã ở trạng thái `Completed` (`ConflictException`). |
| 7 | `LuuDiemDanhTheoBuoiHocAsync_TuChoiKhiBuoiHocBiHuy` | **PASSED** | Ngăn chặn điểm danh buổi học đã bị hủy `Cancelled` (`ConflictException`). |
| 8 | `LuuDiemDanhTheoBuoiHocAsync_TuChoiKhiThieuHocVienHopLe` | **PASSED** | Bắt buộc danh sách điểm danh phải bao phủ đầy đủ tất cả học viên hợp lệ (`ConflictException`). |
| 9 | `LuuDiemDanhTheoBuoiHocAsync_TuChoiKhiChuaEnrollmentKhongHopLeHoacTrangThaiKhacActive` | **PASSED** | Từ chối nếu gửi kèm học viên có trạng thái khác `Active` (ví dụ `Paused`) (`ConflictException`). |
| 10 | `LuuDiemDanhTheoBuoiHocAsync_TuChoiKhiTrungLapEnrollmentTrongRequest` | **PASSED** | Từ chối request có mã ghi danh bị trùng lặp (`ValidationException`). |

Tổng số test trong toàn dự án: **20/20 passed** (10 test Sessions + 10 test Attendances).
