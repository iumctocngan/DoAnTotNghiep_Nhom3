# Ma trận quyền CMS EDU

Tài liệu này cung cấp cái nhìn tổng quan về quyền của bốn role theo code hiện tại. Chi tiết endpoint và ràng buộc nghiệp vụ nằm trong [core-api-design.md](core-api-design.md).

Hệ thống dùng **RBAC + resource scope** theo nguyên tắc **least privilege**. Controller kiểm tra đăng nhập và role; service giới hạn dữ liệu theo người dùng hiện tại, chẳng hạn Teacher chỉ truy cập lớp mình phụ trách. Fallback policy yêu cầu đăng nhập cho mọi endpoint, trừ các endpoint Authentication công khai và OpenAPI trong môi trường Development.

| Nhóm chức năng | Admin | Teacher | Accountant | CustomerCare |
| --- | --- | --- | --- | --- |
| Tài khoản cá nhân | Của mình | Của mình | Của mình | Của mình |
| Staff và role | Quản lý | — | — | — |
| Course, Level, Lesson | Quản lý | Xem | Xem | Xem |
| Lớp và lịch dạy | Quản lý | Lớp phụ trách | Xem | Xem |
| Học viên | Quản lý | Xem theo lớp phụ trách | Xem, giới hạn dữ liệu | Tạo, sửa, xem |
| Guardian | Quản lý | Xem theo lớp phụ trách | Xem guardian đã liên kết | Quản lý |
| Ghi danh | Quản lý | Xem theo lớp phụ trách | Xem, giới hạn dữ liệu | Quản lý nghiệp vụ ghi danh |
| Buổi học | Quản lý | Xem và hoàn tất theo lớp phụ trách | — | Xem |
| Điểm danh | Quản lý | Quản lý theo lớp phụ trách | — | — |
| Nhận xét | Quản lý | Quản lý theo lớp phụ trách | — | Xem |
| Invoice và công nợ | Quản lý | — | Quản lý | Xem |
| Payment | Quản lý | — | Quản lý | — |
| Dashboard Admin | Xem | — | — | — |
| Dashboard Teacher | — | Theo lớp phụ trách | — | — |
| Dashboard kế toán | — | — | Xem | — |
| Dashboard CustomerCare | — | — | — | Xem |

**Quy ước:**

- **Quản lý:** được thực hiện các thao tác nghiệp vụ đã có của nhóm chức năng.
- **Xem:** chỉ có quyền đọc.
- **Lớp phụ trách:** chỉ truy cập lớp hiện do Teacher phụ trách và dữ liệu thuộc lớp đó.
- **Giới hạn dữ liệu:** được xem bản ghi nhưng một số trường nhạy cảm bị ẩn (chi tiết bên dưới).
- **—:** không có quyền.
- **401:** chưa đăng nhập hoặc token không hợp lệ. **403:** đã đăng nhập nhưng không đủ quyền hoặc ngoài phạm vi dữ liệu.

### Chi tiết phạm vi & giới hạn dữ liệu (Data Scope):

1. **Giới hạn dữ liệu của Kế toán (Accountant - Field-level):**
   - **Học viên:** Bị ẩn trường LearningNote (Lưu ý học tập) trả về null.
   - **Ghi danh:** Bị ẩn trường PauseReason (Lý do bảo lưu) và EndReason (Lý do kết thúc) trả về null.
   - **Nhận xét:** Hoàn toàn không có quyền xem nhận xét sư phạm của học viên.
   - **Người giám hộ:** Chỉ xem người giám hộ đã liên kết với học viên để đối soát thu tiền.

2. **Phạm vi "Lớp phụ trách" của Giáo viên (Teacher - Row-level):**
   - **Lớp & Lịch dạy:** Chỉ truy cập các lớp có MainTeacherUserId == currentUser.UserId.
   - **Học viên & Người giám hộ:** Chỉ thấy học viên (và phụ huynh) có ghi danh vào lớp do mình phụ trách.
   - **Buổi học, Điểm danh, Nhận xét:** Chỉ thực hiện trên các buổi học thuộc lớp mình phụ trách.

Quyền không được ghi trong ma trận mặc định bị từ chối theo nguyên tắc **least privilege**.
