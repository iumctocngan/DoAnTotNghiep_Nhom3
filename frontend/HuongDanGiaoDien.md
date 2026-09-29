# Giao diện quản lý trung tâm

Giao diện dùng Arial/Helvetica, thanh đầu đỏ, menu xanh đậm, nền xanh và bảng tiêu đề xanh theo ảnh tham chiếu. Không thể xác định chính xác tên font chỉ từ ảnh.

## Các màn hình bổ sung

| Đường dẫn | Chức năng |
| --- | --- |
| `/hoc-vien` | Tìm kiếm, phân trang, tạo/sửa hồ sơ, lưu trữ/khôi phục |
| `/nguoi-giam-ho` | Tìm kiếm, tạo/sửa/xóa người giám hộ |
| `/hoc-vien/:id/nguoi-giam-ho` | Gắn, sửa, gỡ liên kết và đặt người liên hệ chính |
| `/lop-hoc` | Xem, tạo/sửa/xóa lớp, chọn giáo viên và cấp độ |
| `/ghi-danh` | Ghi danh, bảo lưu, trở lại, hoàn thành, nghỉ học; lọc theo học viên/lớp từ màn hình liên quan |
| `/ghi-danh/:id/nhan-xet` | Xem, tạo/sửa nhận xét; giáo viên truy cập từ bảng điểm danh |

Các màn hình sử dụng `apiClient` hiện có để gửi token và làm mới phiên đăng nhập. Quyền thao tác khớp với controller; máy chủ tiếp tục kiểm tra phạm vi lớp và quyền sở hữu nhận xét. Tên file, biến, hàm mới dùng tiếng Việt không dấu. Tên thuộc tính JSON và các API thư viện giữ nguyên để tương thích backend.

## Chạy và kiểm tra

1. Chạy backend theo README của dự án (API tại `http://localhost:5151`).
2. Trong thư mục frontend, chạy `npm install`, sau đó `npm run dev`.
3. Đăng nhập bằng tài khoản thử nghiệm của từng vai trò.
4. Admin: tạo học viên/người giám hộ, liên kết người giám hộ; tạo lớp với giáo viên và cấp độ hợp lệ; ghi danh học viên vào lớp.
5. Kiểm tra bảo lưu rồi trở lại, thêm/sửa nhận xét; dùng dữ liệu thử riêng để kiểm tra hoàn thành/nghỉ học và xóa.
6. CustomerCare: kiểm tra quản lý hồ sơ và ghi danh; không có quyền tạo/xóa lớp, hoàn thành ghi danh hay tạo nhận xét.
7. Teacher: mở Điểm danh → Nhận xét học viên, kiểm tra quyền lớp phụ trách và nhận xét do mình tạo.
8. Accountant: chỉ xem hồ sơ/lớp/ghi danh trong các màn hình bổ sung; không có nút chỉnh sửa.
9. Kiểm tra tìm kiếm, phân trang, trạng thái rỗng/lỗi và menu thu gọn trên màn hình nhỏ.

`npm run build` kiểm tra TypeScript và tạo bản production. Cần backend, cơ sở dữ liệu và phiên đăng nhập hợp lệ để kiểm chứng đầy đủ các thao tác ghi dữ liệu.
