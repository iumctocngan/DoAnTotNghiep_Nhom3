# Thiết kế API Core Platform — CMS EDU

API MVP dựa trên [overview.md](overview.md) và [core-database-design.md](core-database-design.md).

## 1. Quy ước

- Base URL: `/api`.
- JWT Bearer cho các endpoint nghiệp vụ; access token có thời hạn ngắn và được cấp lại bằng refresh token có rotation.
- `GET`: đọc, `POST`: tạo/action, `PUT`: cập nhật, `DELETE`: chỉ gỡ liên kết guardian không giữ cờ chính.
- API danh sách hỗ trợ `page`, `pageSize`, `search` và filter cần thiết; `pageSize <= 100`.
- Danh sách trả `{ items, page, pageSize, totalItems }`.
- Lỗi dùng `ProblemDetails` của ASP.NET Core.
- Status chính: `200`, `201`, `204`, `400`, `401`, `403`, `404`, `409`.
- Dùng DTO, không trả trực tiếp EF entity.

Trong bảng quyền, **Authenticated** nghĩa là bất kỳ nhân viên đã đăng nhập. **Theo phạm vi role** nghĩa là: Admin xem toàn bộ; Teacher chỉ xem dữ liệu của lớp phụ trách; Accountant chỉ xem dữ liệu học viên cần cho nghiệp vụ học phí; CustomerCare xem dữ liệu học viên, lớp và ghi danh để chăm sóc học viên.

## 2. Phạm vi MVP

Các luồng phải chạy hoàn chỉnh trước:

```text
1. Login và phân quyền
2. Học sinh và phụ huynh
3. Curriculum, lớp và lịch học
4. Ghi danh và bảo lưu
5. Session, điểm danh và nhận xét
6. Invoice, payment và công nợ
7. Dashboard cơ bản
```

Reset password, archive, deactivate danh mục và màn hình tra cứu audit làm sau nếu còn thời gian. Backend vẫn ghi audit ngay từ MVP.

## 3. Authentication và nhân viên

| Method | Endpoint | Mục đích | Quyền |
|---|---|---|---|
| POST | `/api/auth/login` | Nhận JWT | Public |
| POST | `/api/auth/refresh` | Xoay refresh token và cấp access token mới | Public, yêu cầu refresh token hợp lệ |
| POST | `/api/auth/logout` | Thu hồi phiên refresh token hiện tại | Public, yêu cầu refresh token hợp lệ |
| POST | `/api/auth/logout-all` | Thu hồi toàn bộ refresh token của tài khoản | Authenticated |
| GET | `/api/auth/me` | Thông tin bản thân | Authenticated |
| POST | `/api/auth/change-password` | Nhân viên đổi mật khẩu bằng mật khẩu hiện tại | Authenticated |
| GET | `/api/staff` | Danh sách nhân viên | Admin |
| GET | `/api/staff/{id}` | Chi tiết nhân viên | Admin |
| POST | `/api/staff` | Tạo tài khoản | Admin |
| PUT | `/api/staff/{id}` | Sửa họ tên, email và số điện thoại | Admin |
| PUT | `/api/staff/{id}/role` | Đổi role | Admin |
| POST | `/api/staff/{id}/reset-password` | Đặt lại mật khẩu | Admin |
| POST | `/api/staff/{id}/deactivate` | Ngừng tài khoản | Admin |
| POST | `/api/staff/{id}/activate` | Kích hoạt tài khoản | Admin |

Bốn role cố định; mỗi user có một role chính. Admin không được tự đổi role hoặc tự deactivate; hệ thống luôn phải còn ít nhất một Admin Active. Không deactivate hoặc đổi role Teacher khi user còn phụ trách lớp Active. Admin được đổi role của tài khoản Inactive và role mới có hiệu lực khi tài khoản được activate lại. Tài khoản mới luôn là Active; request tạo không nhận trạng thái và email được đánh dấu đã xác nhận. Tài khoản Inactive không đăng nhập hoặc refresh được nhưng Admin vẫn có thể reset mật khẩu trước khi activate. Khi đổi email, deactivate, đổi role hoặc reset password, backend thu hồi toàn bộ refresh token của tài khoản; access token đã cấp còn hiệu lực tối đa 15 phút. Nhân viên tự đổi mật khẩu sẽ bị đăng xuất khỏi tất cả thiết bị và phải đăng nhập lại. Admin nhập EmployeeCode khi tạo tài khoản; mã phải unique và không được sửa hoặc tái sử dụng. Email đăng nhập được phép thay đổi nếu không trùng và phải đồng bộ với UserName. Admin nhập mật khẩu tạm khi tạo tài khoản.

Danh sách Staff mặc định gồm cả tài khoản Active và Inactive, hỗ trợ lọc theo trạng thái, role và tìm theo mã nhân viên, họ tên hoặc email. Admin được sửa họ tên, email và số điện thoại của chính mình nhưng không được tự đổi role hoặc tự deactivate. Số điện thoại không bắt buộc và không unique. Activate giữ nguyên role; reset mật khẩu giữ nguyên trạng thái tài khoản. Activate tài khoản đang Active hoặc deactivate tài khoản đang Inactive vẫn trả `204` và không ghi AuditLog mới. Tạo tài khoản, đổi role, activate, deactivate và reset mật khẩu đều ghi AuditLog khi dữ liệu thực sự thay đổi; log không chứa mật khẩu.

Login trả access token trong response; refresh token được đặt trong cookie `HttpOnly`, `Secure` và `SameSite`. Database chỉ lưu SHA-256 hash của refresh token, không lưu token thô. Mỗi lần refresh phải thu hồi token cũ, phát token mới trong cùng family và ghi liên kết thay thế. Nếu một token đã xoay vòng bị dùng lại, backend coi đó là dấu hiệu rò rỉ và thu hồi toàn bộ token trong family. Thời hạn access token và refresh token lấy từ cấu hình; mặc định thiết kế là 15 phút và 7 ngày.

## 4. Học sinh và phụ huynh

| Method | Endpoint                                    | Mục đích                             | Quyền                   |
| ------ | ------------------------------------------- | ------------------------------------ | ----------------------- |
| GET    | `/api/students`                             | Danh sách/tìm kiếm                   | Theo phạm vi role       |
| GET    | `/api/students/{id}`                        | Chi tiết                             | Theo phạm vi role       |
| POST   | `/api/students`                             | Tạo học sinh                         | Admin, CustomerCare     |
| PUT    | `/api/students/{id}`                        | Sửa hồ sơ                            | Admin, CustomerCare     |
| POST   | `/api/students/{id}/archive`                | Lưu trữ hồ sơ                        | Admin                   |
| POST   | `/api/students/{id}/restore`                | Khôi phục hồ sơ đã lưu trữ           | Admin                   |
| GET    | `/api/students/{id}/guardians`              | Người giám hộ                        | Theo quyền xem học sinh |
| POST   | `/api/students/{id}/guardians`              | Tạo/gắn người giám hộ                | Admin, CustomerCare     |
| PUT    | `/api/students/{id}/guardians/{guardianId}` | Sửa quan hệ hoặc chọn guardian chính | Admin, CustomerCare     |
| DELETE | `/api/students/{id}/guardians/{guardianId}` | Gỡ liên kết                          | Admin, CustomerCare     |

Giáo viên chỉ xem học sinh thuộc lớp phụ trách. Không archive học sinh có enrollment Active hoặc Paused. Một học sinh có tối đa một guardian chính; guardian này là đầu mối liên hệ và đóng học phí. Đổi guardian chính thực hiện trong transaction. Không gỡ guardian đang giữ cờ chính khi chưa chọn người thay thế.

## 5. Curriculum

| Method | Endpoint                       | Mục đích                     | Quyền         |
| ------ | ------------------------------ | ---------------------------- | ------------- |
| GET    | `/api/courses`                 | Danh sách course             | Authenticated |
| GET    | `/api/courses/{id}`            | Chi tiết course              | Authenticated |
| POST   | `/api/courses`                 | Tạo course                   | Admin         |
| PUT    | `/api/courses/{id}`            | Sửa course                   | Admin         |
| POST   | `/api/courses/{id}/deactivate` | Ngừng áp dụng                | Admin         |
| GET    | `/api/levels?courseId=`        | Danh sách level              | Authenticated |
| GET    | `/api/levels/{id}`             | Chi tiết level               | Authenticated |
| POST   | `/api/levels`                  | Tạo level                    | Admin         |
| PUT    | `/api/levels/{id}`             | Sửa level                    | Admin         |
| POST   | `/api/levels/{id}/deactivate`  | Ngừng áp dụng                | Admin         |
| GET    | `/api/lessons?levelId=`        | Danh sách lesson thuộc level | Authenticated |
| GET    | `/api/lessons/{id}`            | Chi tiết lesson              | Authenticated |
| POST   | `/api/lessons`                 | Tạo lesson                   | Admin         |
| PUT    | `/api/lessons/{id}`            | Sửa lesson                   | Admin         |
| POST   | `/api/lessons/{id}/deactivate` | Ngừng áp dụng                | Admin         |

Danh mục đã được sử dụng không có API DELETE.

Lesson trực tiếp thuộc Level; request tạo hoặc cập nhật lesson dùng `LevelId`.

## 6. Lớp và lịch học

| Method | Endpoint                     | Mục đích               | Quyền             |
| ------ | ---------------------------- | ---------------------- | ----------------- |
| GET    | `/api/classes`               | Danh sách lớp          | Theo phạm vi role |
| GET    | `/api/classes/{id}`          | Chi tiết lớp           | Theo phạm vi role |
| POST   | `/api/classes`               | Tạo lớp                | Admin             |
| PUT    | `/api/classes/{id}`          | Sửa lớp                | Admin             |
| POST   | `/api/classes/{id}/activate` | Mở lớp                 | Admin             |
| POST   | `/api/classes/{id}/complete` | Kết thúc lớp           | Admin             |
| POST   | `/api/classes/{id}/cancel`   | Hủy lớp chưa hoạt động | Admin             |

Request tạo hoặc sửa lớp chứa `DayOfWeek`, `StartTime` và `EndTime` cho một lịch học cố định mỗi tuần. Không giảm Capacity thấp hơn tổng enrollment `Active/Paused`. Không complete lớp khi còn một trong hai trạng thái này; complete lớp không tự động complete enrollment. Khi sửa lịch lớp phải kiểm tra giờ và trùng lịch; session cũ không bị thay đổi.

## 7. Ghi danh

| Method | Endpoint                         | Mục đích         | Quyền                           |
| ------ | -------------------------------- | ---------------- | ------------------------------- |
| GET    | `/api/enrollments`               | Danh sách        | Admin, Accountant, CustomerCare |
| GET    | `/api/enrollments/{id}`          | Chi tiết         | Theo phạm vi role               |
| POST   | `/api/enrollments`               | Ghi danh/xếp lớp | Admin, CustomerCare             |
| POST   | `/api/enrollments/{id}/pause`    | Bảo lưu          | Admin, CustomerCare             |
| POST   | `/api/enrollments/{id}/resume`   | Học lại          | Admin, CustomerCare             |
| POST   | `/api/enrollments/{id}/complete` | Hoàn thành       | Admin                           |
| POST   | `/api/enrollments/{id}/withdraw` | Nghỉ học         | Admin, CustomerCare             |

Mỗi học sinh chỉ có tối đa một enrollment mang trạng thái `Active` hoặc `Paused`; cả hai đều tính vào Capacity. Ghi danh yêu cầu học sinh có guardian chính. Pause chỉ áp dụng cho enrollment `Active`, bắt buộc có lý do và không tự động thay đổi invoice; resume chỉ áp dụng cho enrollment `Paused`. Complete chỉ yêu cầu ngày kết thúc; withdraw yêu cầu ngày kết thúc và lý do, đồng thời giữ nguyên invoice, payment và công nợ đã phát sinh. Không xóa enrollment; trường hợp nhập nhầm dùng `withdraw` với lý do phù hợp. Học sinh tiếp tục học sau `Completed/Withdrawn` phải được tạo enrollment mới.

## 8. Session, điểm danh và nhận xét

| Method | Endpoint                        | Mục đích                   | Quyền                        |
| ------ | ------------------------------- | -------------------------- | ---------------------------- |
| GET    | `/api/sessions`                 | Danh sách session          | Theo phạm vi role            |
| GET    | `/api/sessions/{id}`            | Chi tiết session           | Theo phạm vi role            |
| POST   | `/api/sessions`                 | Tạo thủ công theo lịch lớp | Admin                        |
| PUT    | `/api/sessions/{id}`            | Sửa session Scheduled      | Admin                        |
| POST   | `/api/sessions/{id}/cancel`     | Hủy session                | Admin                        |
| POST   | `/api/sessions/{id}/complete`   | Xác nhận đã diễn ra        | Admin, Teacher chính của lớp |
| GET    | `/api/sessions/{id}/attendance` | Danh sách điểm danh        | Admin, Teacher chính của lớp |
| PUT    | `/api/sessions/{id}/attendance` | Lưu điểm danh cả lớp       | Admin, Teacher chính của lớp |
| GET    | `/api/enrollments/{id}/remarks` | Danh sách nhận xét         | Theo phạm vi role            |
| POST   | `/api/enrollments/{id}/remarks` | Tạo nhận xét               | Admin, Teacher chính của lớp |
| PUT    | `/api/remarks/{id}`             | Sửa nhận xét               | Người tạo, Admin             |

Request tạo session không nhận `TeacherUserId`; giáo viên được suy ra từ giáo viên chính của lớp và MVP chưa hỗ trợ dạy thay. Attendance PUT gửi toàn bộ danh sách enrollment hợp lệ của session với trạng thái `Present` hoặc `Absent` và lưu đồng thời trong một transaction; lý do vắng nếu cần ghi trong `Note`. Enrollment hợp lệ phải thuộc cùng lớp, có trạng thái `Active`, `StartDate <= SessionDate` và chưa có `EndDate` hoặc `EndDate >= SessionDate`; enrollment `Paused/Completed/Withdrawn` không nằm trong danh sách điểm danh. Trước lần lưu đầu tiên, không có bản ghi attendance nghĩa là chưa điểm danh. Endpoint complete chỉ thành công khi mọi enrollment hợp lệ tại ngày học đã có attendance; nếu còn thiếu, API trả lỗi kèm danh sách enrollment chưa được điểm danh. Admin hoặc Teacher chính của lớp được sửa attendance khi session chưa `Completed`; sau khi hoàn tất, không cho sửa, hủy hoặc mở lại session và attendance trong MVP. Lesson của session, nếu có, phải thuộc cùng level với lớp. Không tạo remark mới cho enrollment `Completed/Withdrawn`.

## 9. Học phí

| Method | Endpoint                      | Mục đích                    | Quyền                           |
| ------ | ----------------------------- | --------------------------- | ------------------------------- |
| GET    | `/api/invoices`               | Danh sách khoản phải thu    | Admin, Accountant, CustomerCare |
| GET    | `/api/invoices/{id}`          | Chi tiết invoice/payment    | Admin, Accountant, CustomerCare |
| POST   | `/api/invoices`               | Lập kỳ học phí              | Admin, Accountant               |
| PUT    | `/api/invoices/{id}`          | Sửa invoice chưa có payment | Admin, Accountant               |
| POST   | `/api/invoices/{id}/cancel`   | Hủy invoice                 | Admin, Accountant               |
| GET    | `/api/payments`               | Danh sách khoản thu         | Admin, Accountant               |
| GET    | `/api/payments/{id}`          | Chi tiết/phiếu thu          | Admin, Accountant               |
| POST   | `/api/invoices/{id}/payments` | Ghi nhận payment            | Admin, Accountant               |
| POST   | `/api/payments/{id}/cancel`   | Hủy payment                 | Admin, Accountant               |

Backend sinh `InvoiceNumber`, `PaymentNumber`, `ReceiptNumber`. Chỉ enrollment `Active` được lập invoice mới; `AmountDue > 0`, kỳ phải đúng 6 tháng, `DueDate` không sau `PeriodEnd` và các kỳ không chồng lấn theo học sinh. Request hủy invoice hoặc payment đều bắt buộc `reason`; backend lưu trạng thái `Cancelled`, người hủy, thời gian hủy và AuditLog trong cùng transaction. Không hủy invoice khi còn payment `Confirmed`; phải hủy các payment này trước.

## 10. Dashboard cơ bản

| Method | Endpoint                       | Mục đích          | Quyền        |
| ------ | ------------------------------ | ----------------- | ------------ |
| GET    | `/api/dashboard/admin`         | Dashboard Admin   | Admin        |
| GET    | `/api/dashboard/teacher`       | Dashboard Teacher | Teacher      |
| GET    | `/api/dashboard/accounting`    | Dashboard kế toán | Accountant   |
| GET    | `/api/dashboard/customer-care` | Dashboard CSKH    | CustomerCare |

Dashboard MVP trả số liệu cơ bản, danh sách học sinh Active chưa có invoice và công nợ quá hạn, được tính trực tiếp từ dữ liệu nghiệp vụ. Cảnh báo tái phí trong 30 ngày và thống kê vắng nhiều theo 10 session gần nhất được hoãn. Các màn hình đầy đủ tiếp tục dùng API danh sách hiện có với filter phù hợp, không tạo resource hoặc bảng thống kê riêng.

Backend vẫn tự ghi audit cho thao tác quan trọng, nhưng màn hình và API tra cứu audit được hoãn sau MVP.

## 11. Chuyển trạng thái

```text
Class:      Preparing → Active → Completed
            Preparing → Cancelled

Enrollment: Active → Paused → Active
            Active/Paused → Withdrawn
            Active → Completed

Session:    Scheduled → Completed hoặc Cancelled
Invoice:    Issued → Cancelled
Payment:    Confirmed → Cancelled
```

Trạng thái kết thúc không chuyển ngược. Dời lịch chỉ sửa session Scheduled.

## 12. Transaction bắt buộc

1. Đổi guardian chính.
2. Lưu attendance theo lô.
3. Hủy invoice/payment và tạo audit tương ứng.
4. Xoay refresh token và phát hiện tái sử dụng token.

## 13. Ngoài phạm vi

- Tài khoản phụ huynh/học sinh và đăng nhập qua nhà cung cấp bên ngoài.
- Nhiều cơ sở, học thử, học bù, quyền học theo buổi.
- Thanh toán online và thông báo đa kênh.
- Upload file, CLO/Rubric và API nội bộ AI.
- Lịch sử liên hệ phụ huynh, import/export và báo cáo nâng cao.
- Giáo viên dạy thay, phân biệt vắng có phép/không phép, cảnh báo tái phí 30 ngày và thống kê vắng nhiều.
