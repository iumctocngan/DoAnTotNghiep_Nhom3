# Thiết kế phân quyền CMS EDU

## Phạm vi thiết kế

Hệ thống sử dụng mô hình:

**RBAC + resource scope**, áp dụng nguyên tắc **least privilege**.

- RBAC quyết định role nào được gọi một chức năng hoặc endpoint.
- Resource scope giới hạn các bản ghi và trường dữ liệu role được phép xem hoặc thao tác.
- Least privilege yêu cầu chỉ cấp đúng quyền cần thiết; quyền không khai báo thì từ chối.

## Role và chức năng

✓: được phép; —: không được phép; P: theo phạm vi dữ liệu.

| Chức năng | Admin | Teacher | Accountant | CustomerCare |
|---|:---:|:---:|:---:|:---:|
| Tài khoản cá nhân; xem Course/Level/Lesson | ✓ | ✓ | ✓ | ✓ |
| Quản lý Staff, danh mục, lớp; tạo/sửa/hủy buổi học | ✓ | — | — | — |
| Tra cứu lớp phụ trách và lịch dạy của Teacher | ✓ | P | — | — |
| Xem học viên, guardian của học viên, lớp, chi tiết ghi danh | ✓ | P | P | ✓ |
| Tạo/sửa học viên; tra cứu/tạo/sửa guardian, quản lý liên kết | ✓ | — | — | ✓ |
| Lưu trữ/khôi phục học viên; xóa hồ sơ guardian chưa liên kết | ✓ | — | — | — |
| Xem danh sách ghi danh | ✓ | — | P | ✓ |
| Tạo/bảo lưu/học lại/nghỉ học ghi danh | ✓ | — | — | ✓ |
| Hoàn tất ghi danh | ✓ | — | — | — |
| Xem buổi học, nhận xét | ✓ | P | — | P |
| Hoàn tất buổi học, điểm danh, tạo/sửa nhận xét* | ✓ | P | — | — |
| Xem invoice và payment liên quan | ✓ | — | ✓ | P |
| Tạo/sửa/hủy invoice; xem/tạo/hủy payment | ✓ | — | ✓ | — |
| Dashboard riêng của role | ✓ | P | ✓ | ✓ |

- *Teacher chỉ sửa nhận xét mình tạo khi vẫn phụ trách lớp.*
- Teacher/Accountant xem guardian qua học viên, không được tra cứu danh bạ `/guardians` riêng.
- CSKH được gỡ liên kết guardian, nhưng không xóa hồ sơ guardian; không gọi API `/payments` riêng.
- Mỗi role chỉ truy cập dashboard của mình, kể cả Admin. Quyền cụ thể theo endpoint xem [core-api-design.md](core-api-design.md).

## Resource scope

| Role | Phạm vi dữ liệu |
|---|---|
| Admin | Toàn bộ dữ liệu của chức năng được cấp quyền. |
| Teacher | Lớp hiện phụ trách: `Class.MainTeacherUserId = UserId` từ JWT; không giữ quyền theo phân công cũ. |
| Accountant | Dữ liệu phục vụ học phí; không xem ghi chú học tập, điểm danh, nhận xét. Chứng từ không giới hạn theo người tạo. |
| CustomerCare | Dữ liệu chăm sóc học viên và tài chính cần thiết; không ghi chứng từ. |

Service lọc phạm vi trước đếm/phân trang và cả khi lấy chi tiết; kiểm tra quan hệ parent–child bằng DB, không tin ID client gửi. DTO chỉ trả trường được phép; người phụ trách module chốt cụ thể các trường tài chính. Mọi role, kể cả Admin, vẫn tuân thủ nghiệp vụ.

## Thực thi

- **API:** fallback policy yêu cầu đăng nhập mặc định. Endpoint public phải khai báo `[AllowAnonymous]`; endpoint nghiệp vụ gắn policy hoặc `[Authorize(Roles = ...)]` để kiểm tra quyền cụ thể.
- **Service:** dùng `ICurrentUser` và DB để kiểm tra phạm vi; thao tác tài khoản cá nhân lấy UserId từ JWT.
- **Tài khoản:** một trong bốn role cố định, không có bảng Permission hay CRUD định nghĩa role. Login/refresh yêu cầu Active; thay email/role, đổi/reset mật khẩu hoặc deactivate thu hồi refresh token, JWT cũ còn tới hạn (mặc định 15 phút).


## Policy và trạng thái triển khai

| Policy | Role | Trạng thái |
|---|---|---|
| `Auth.SelfService` | Bốn role | Đã triển khai cho `me`, `change-password`, `logout-all` |
| `Staff.Manage` | Admin | Đã triển khai cho toàn bộ `/api/staff` |
| `Teachers.Classes.Read` | Admin, Teacher | Đã triển khai; Teacher chỉ xem chính mình |
| `Teachers.Schedule.Read` | Admin, Teacher | Đã triển khai; Teacher chỉ xem chính mình |
| `Catalog.Read` | Bốn role | Đã triển khai cho GET Course, Level và Lesson |
| `Catalog.Manage` | Admin | Đã triển khai cho tạo, sửa và deactivate Course, Level, Lesson |
| `Students.Read`, `Students.Write`, `Students.Archive` | Theo bảng role | Tên dự kiến; code đã có role check và lọc Teacher, ẩn LearningNote với Accountant |
| `Guardians.Directory.Read`, `Guardians.Write`, `Guardians.Delete` | Danh bạ/ghi: Admin, CustomerCare; xóa: Admin | Tên dự kiến; code đã có kiểm tra role |
| `StudentGuardians.Read`, `StudentGuardians.Write` | Đọc: bốn role theo phạm vi; ghi: Admin, CustomerCare | Tên dự kiến; code đã có kiểm tra role và phạm vi Teacher |
| `Sessions.Read`, `Sessions.Manage`, `Sessions.Complete` | Theo bảng role | Tên dự kiến; code đã lọc Teacher, chặn Accountant đọc, kiểm tra giáo viên khi complete |
| Class, Enrollment, Attendance, Remark | Theo bảng role | Chờ endpoint hoặc người phụ trách module áp dụng |
| Invoice, Payment, Dashboard | Theo bảng role | Chờ endpoint tương ứng |

Mỗi module tự gắn policy vào endpoint và tự triển khai resource scope trong service. Phần authorization dùng chung không chứa trạng thái hoặc quy tắc nghiệp vụ riêng của module.

`Auth.SelfService`, `Staff.Manage`, `Teachers.Classes.Read`, `Teachers.Schedule.Read`, `Catalog.Read` và `Catalog.Manage` đã được định nghĩa và đăng ký dưới dạng policy chức năng. Các tên dự kiến phải được thêm vào constants và đăng ký trong `AddAuthorization` trước khi dùng; gắn tên chưa đăng ký sẽ gây lỗi. Không coi việc chưa dùng named policy là chưa có phân quyền.

### API Teacher

| Method | Endpoint | Quyền |
|---|---|---|
| GET | `/api/teachers/{teacherId}/classes` | Admin hoặc chính Teacher đó |
| GET | `/api/teachers/{teacherId}/schedule` | Admin hoặc chính Teacher đó |

Danh sách lớp được lọc bằng `Class.MainTeacherUserId`. Lịch dạy lấy từ Session của các lớp Teacher hiện phụ trách; không giữ quyền theo phân công cũ. Hai API hỗ trợ phân trang; lịch dạy hỗ trợ lọc `fromDate`, `toDate`.

## Trách nhiệm và kiểm chứng trước bàn giao

- Minh: nền Authentication/RBAC, Staff, ràng buộc quản lý tài khoản, Teacher lookup và Course/Level/Lesson; Dashboard làm sau khi các module liên quan hoàn thành.
- Người phụ trách Student/Guardian, Class/Enrollment, Session/Attendance/Remark, Finance: gắn quyền chức năng và thực hiện lọc phạm vi, quan hệ, trạng thái trong module của mình. Không cần một engine nghiệp vụ chung.
