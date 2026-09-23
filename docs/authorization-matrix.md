# Ma trận quyền CMS EDU

Tài liệu này cung cấp cái nhìn tổng quan về quyền của bốn role theo code hiện tại. Chi tiết từng hành động, giới hạn trường dữ liệu và ràng buộc nghiệp vụ nằm trong [authorization-design.md](authorization-design.md).

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
| Dashboard kế toán | Xem | — | Xem | — |
| Dashboard CustomerCare | — | — | — | Xem |

**Quy ước:**

- **Quản lý:** được thực hiện các thao tác nghiệp vụ đã có của nhóm chức năng.
- **Xem:** chỉ có quyền đọc.
- **Lớp phụ trách:** chỉ truy cập lớp hiện do Teacher phụ trách và dữ liệu thuộc lớp đó.
- **Giới hạn dữ liệu:** được xem bản ghi nhưng một số trường nhạy cảm bị ẩn.
- **—:** không có quyền.

Quyền không được ghi trong ma trận mặc định bị từ chối theo nguyên tắc **least privilege**.
