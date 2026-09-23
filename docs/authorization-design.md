# Thiết kế phân quyền CMS EDU

## Quy tắc chung

**Mô hình phân quyền: RBAC + resource scope.** Áp dụng nguyên tắc **least privilege**.

- RBAC quyết định role nào được gọi một chức năng hoặc endpoint.
- Resource scope giới hạn các bản ghi và trường dữ liệu role được phép xem hoặc thao tác.
- Least privilege yêu cầu chỉ cấp đúng quyền cần thiết; quyền không khai báo thì từ chối.

## Quyền đang được triển khai

Bảng mô tả **code hiện tại**. `—` là không có quyền; `✓` là được thực hiện hành động. Với quyền xem, **Tất cả** nghĩa là không lọc theo người tạo hoặc nhân viên phụ trách; **Lớp phụ trách** chỉ gồm lớp hiện do Teacher phụ trách và dữ liệu thuộc lớp đó; **Của mình** là tài khoản hoặc nội dung của chính người dùng.

| Tài nguyên | Hành động | Admin | Teacher | Accountant | CustomerCare |
| --- | --- | --- | --- | --- | --- |
| Tài khoản | Đăng nhập, xem tài khoản mình, đổi mật khẩu, đăng xuất | Của mình | Của mình | Của mình | Của mình |
| Staff | Tạo, sửa, đổi role, reset mật khẩu, activate/deactivate | ✓ | — | — | — |
| Course / Level / Lesson | Xem | Tất cả | Tất cả | Tất cả | Tất cả |
| Course / Level / Lesson | Tạo, sửa, ngừng áp dụng | ✓ | — | — | — |
| Teacher | Tra cứu lớp phụ trách, lịch dạy | Tất cả | Của mình | — | — |
| Học viên | Xem danh sách, chi tiết | Tất cả | Lớp phụ trách | Tất cả¹ | Tất cả |
| Học viên | Tạo, sửa | ✓ | — | — | ✓ |
| Học viên | Lưu trữ, khôi phục | ✓ | — | — | — |
| Guardian | Xem danh bạ `/api/guardians` | Tất cả | — | — | Tất cả |
| Guardian | Tạo, sửa | ✓ | — | — | ✓ |
| Guardian | Xóa khi chưa liên kết | ✓ | — | — | — |
| Guardian gắn với học viên | Xem | Tất cả | Lớp phụ trách | Tất cả¹ | Tất cả |
| Guardian gắn với học viên | Gắn, sửa, gỡ liên kết | ✓ | — | — | ✓ |
| Lớp | Xem danh sách, chi tiết | Tất cả | Lớp phụ trách | Tất cả | Tất cả |
| Lớp | Tạo, sửa, xóa | ✓ | — | — | — |
| Ghi danh | Xem danh sách | Tất cả | — | Tất cả² | Tất cả |
| Ghi danh | Xem chi tiết | Tất cả | Lớp phụ trách | Tất cả² | Tất cả |
| Ghi danh | Tạo, bảo lưu, học lại, nghỉ học | ✓ | — | — | ✓ |
| Ghi danh | Hoàn tất | ✓ | — | — | — |
| Buổi học | Xem danh sách, chi tiết | Tất cả | Lớp phụ trách | — | Tất cả |
| Buổi học | Tạo, sửa, hủy | ✓ | — | — | — |
| Buổi học | Hoàn tất | ✓ | Lớp phụ trách | — | — |
| Điểm danh | Xem, lưu | Tất cả | Lớp phụ trách | — | — |
| Nhận xét | Xem | Tất cả | Lớp phụ trách | — | Tất cả |
| Nhận xét | Tạo | ✓ | Lớp phụ trách | — | — |
| Nhận xét | Sửa | ✓ | Của mình trong lớp phụ trách | — | — |
| Invoice, công nợ | Xem | Tất cả | — | Tất cả | Tất cả³ |
| Invoice | Tạo, hủy | ✓ | — | ✓ | — |
| Payment `/api/payments` | Xem | Tất cả | — | Tất cả | — |
| Payment `/api/payments` | Tạo, hủy | ✓ | — | ✓ | — |
| Dashboard Admin | Xem | Tất cả | — | — | — |
| Dashboard Teacher | Xem | — | Lớp phụ trách | — | — |
| Dashboard kế toán | Xem | Tất cả | — | Tất cả | — |
| Dashboard CustomerCare | Xem | — | — | — | Tất cả |

¹ Accountant xem toàn bộ học viên nhưng `LearningNote = null`; guardian gắn với học viên vẫn gồm họ tên, điện thoại, email và quan hệ. ² Trong phản hồi ghi danh cho Accountant, lý do bảo lưu và kết thúc là `null`. ³ CustomerCare xem toàn bộ invoice, gồm số tiền, số đã thanh toán, công nợ, ghi chú, người tạo, người hủy và lý do hủy; không được gọi API Payment riêng. CustomerCare cũng xem `Note` của buổi học và nội dung nhận xét.

Admin chỉ đổi role sau khi tài khoản nhân viên đã Inactive. Không được deactivate Admin Active cuối cùng; không được deactivate hoặc đổi role Teacher còn phụ trách lớp Active. Các ràng buộc này được kiểm tra tại `StaffService`, ngoài kiểm tra role của controller.

Bốn dashboard theo role đã được triển khai.

## Cách thực thi

- `Program.cs` đặt fallback policy yêu cầu đăng nhập. Chỉ login, refresh, logout một phiên và OpenAPI ở môi trường Development có `[AllowAnonymous]` hoặc `AllowAnonymous()`.
- `Auth.SelfService` dùng cho `me`, `change-password`, `logout-all`; `Staff.Manage` cho toàn bộ `/api/staff`; `Teachers.Classes.Read` và `Teachers.Schedule.Read` cho hai API Teacher; `Catalog.Read`/`Catalog.Manage` cho Course, Level, Lesson. Đây là các named policy đã đăng ký.
- Các controller Student, Guardian, Class, Enrollment, Session, Attendance, Remark, Invoice, Payment dùng `[Authorize(Roles = ...)]` ở action/controller hoặc kiểm tra role tại service như bảng trên. Chúng không dùng những tên policy `Students.Read`, `Sessions.Read`... vì các tên đó chưa được đăng ký; không thêm `[Authorize(Policy = ...)]` với tên chưa đăng ký.
- Service kiểm tra quan hệ Teacher–Class và lọc danh sách trước khi đếm/phân trang; chi tiết cũng phải kiểm tra cùng quan hệ. `ICurrentUser` lấy UserId và role từ JWT, không lấy từ request.
- Login/refresh yêu cầu tài khoản Active. Đổi email, đổi role, đổi/reset mật khẩu hoặc deactivate thu hồi refresh token; access token đã cấp vẫn dùng được đến khi hết hạn, mặc định tối đa 15 phút. Không có bảng Permission hay CRUD tạo/sửa/xóa role.

## Phần chưa triển khai

- Chưa có kiểm thử HTTP end-to-end cho Payment. Unit test Payment đã có; kết quả unit test không thay thế kiểm tra role qua HTTP.
