# Tổng quan nghiệp vụ hệ thống CMS EDU

## 1. Bối cảnh và phạm vi

Đây là đồ án tốt nghiệp xây dựng hệ thống quản lý cho **CMS EDU**, trung tâm dạy toán tư duy tại Hà Nội dành cho trẻ từ **3 đến 11 tuổi**.

Phạm vi đã chốt:

- Trung tâm có một cơ sở.
- Hệ thống chỉ dành cho nhân viên nội bộ.
- Học sinh và phụ huynh không có tài khoản.
- Core dùng kiến trúc monolith và cung cấp dữ liệu chung cho ba module AI.

## 2. Vai trò

| Vai trò | Nghiệp vụ chính |
|---|---|
| Admin | Tài khoản, danh mục, lớp và toàn bộ hệ thống |
| Teacher | Lớp phụ trách, session, điểm danh và nhận xét |
| Accountant | Invoice, payment, phiếu thu và công nợ |
| Customer Care | Hồ sơ học viên, ghi danh và theo dõi tình trạng học |

Mỗi tài khoản có một role chính. Giáo viên chỉ thao tác trên lớp được phân công. Không deactivate hoặc đổi role Teacher khi nhân viên còn phụ trách lớp Active.

Các tên tiếng Anh như `Active`, `Paused`, `Completed` là giá trị trạng thái dùng thống nhất trong code; phần mô tả tiếng Việt giải thích ý nghĩa nghiệp vụ của chúng.

## 3. Mô hình nghiệp vụ

### Chương trình học

```text
Course → Level → Lesson
```

Mỗi lesson trực tiếp thuộc một level. Danh mục đã được sử dụng không bị xóa, chỉ chuyển sang ngừng áp dụng.

### Tổ chức học

```text
Student → Enrollment → Class → Session → Attendance
```

- `Enrollment` lưu lịch sử học sinh tham gia lớp.
- Không lưu `CurrentClassId` trên học sinh.
- Trong phạm vi đồ án, tại một thời điểm mỗi học sinh chỉ có tối đa một enrollment mang trạng thái `Active` hoặc `Paused`.
- Tình trạng đang học, bảo lưu, hoàn thành hoặc nghỉ học được xác định từ enrollment.

## 4. Vòng đời học viên

```text
Tạo hồ sơ học sinh và phụ huynh
→ Chọn chương trình
→ Ghi danh và xếp lớp
→ Thu học phí 6 tháng
→ Học, điểm danh và nhận xét
→ Tái phí / bảo lưu / kết thúc
```

Quy tắc:

- Enrollment lưu lý do và ngày dự kiến quay lại của lần bảo lưu hiện tại. Bảo lưu không tự động thay đổi invoice.
- Hoàn thành phải lưu ngày kết thúc; nghỉ học phải lưu ngày kết thúc và lý do.
- Học sinh quay lại sau khi nghỉ hoặc học level mới sau khi hoàn thành phải tạo enrollment mới.
- Chỉ xóa enrollment nhập nhầm khi chưa có điểm danh, nhận xét hoặc học phí.

## 5. Quản lý con người

### Học sinh và phụ huynh

- Lưu mã học sinh, họ tên, ngày sinh và lưu ý học tập.
- Một học sinh có thể có nhiều người giám hộ.
- Xác định một người liên hệ chính và một người thanh toán chính.
- Phải có người liên hệ chính và người thanh toán chính trước khi ghi danh.

### Giáo viên

- Lưu hồ sơ và trạng thái làm việc.
- Mỗi lớp có một giáo viên chính.
- Một session có thể ghi nhận giáo viên thực tế đứng lớp.
- Không quản lý trợ giảng.

## 6. Lớp, lịch học và điểm danh

### Lớp và lịch

- Lớp có level, giáo viên, sĩ số tối đa và một lịch học cố định mỗi tuần.
- Trạng thái lớp: Preparing, Active, Completed, Cancelled.
- Chỉ lớp Active và còn chỗ mới nhận enrollment.
- Enrollment Active và Paused đều tính vào sĩ số; không giảm Capacity thấp hơn sĩ số này.
- Không hoàn thành lớp khi còn enrollment Active hoặc Paused; việc hoàn thành lớp không tự động hoàn thành enrollment.
- Admin tạo session thủ công dựa trên lịch của lớp.
- Session có trạng thái Scheduled, Completed hoặc Cancelled.
- Có thể đổi thời gian của session Scheduled mà không đổi lịch cố định của lớp.
- Hệ thống kiểm tra trùng lịch giáo viên.

### Điểm danh và nhận xét

- Enrollment hợp lệ để điểm danh phải thuộc cùng lớp, có trạng thái Active, `StartDate <= SessionDate` và chưa có `EndDate` hoặc `EndDate >= SessionDate`. Enrollment Paused, Completed hoặc Withdrawn không xuất hiện trong danh sách điểm danh.
- Mỗi enrollment có tối đa một attendance trong một session.
- Trạng thái: Present, ExcusedAbsent hoặc UnexcusedAbsent.
- Không có attendance nghĩa là chưa điểm danh.
- Session Cancelled không được tính chuyên cần.
- Chỉ hoàn tất session khi mọi enrollment hợp lệ của buổi học đã có attendance.
- Chỉ được sửa attendance khi session chưa Completed; hoàn tất session sẽ khóa điểm danh.
- Core lưu lesson đã dạy và nhận xét chính thức của giáo viên.
- Không tạo nhận xét mới cho enrollment Completed hoặc Withdrawn.

Bài tập, tệp, kết quả chi tiết, CLO và rubric được bổ sung khi tích hợp module AI tương ứng.

## 7. Học phí

```text
Enrollment → Invoice kỳ 6 tháng → Payment có số phiếu thu → Công nợ
```

- Một kỳ học phí kéo dài đúng 6 tháng.
- Chỉ enrollment Active được lập invoice mới và số tiền phải lớn hơn 0 VND.
- DueDate không được sau ngày kết thúc kỳ học phí.
- Một invoice có thể được thanh toán nhiều lần.
- Công nợ = số phải thu − tổng payment Confirmed.
- Doanh thu tính từ payment Confirmed, không tính từ invoice.
- Không xóa chứng từ; hủy phải có lý do và audit.
- Tái phí dựa trên kỳ invoice gần nhất của học sinh.
- Nghỉ học không tự động hủy invoice, payment hoặc công nợ đã phát sinh.
- Điểm danh không làm thay đổi học phí.

## 8. Theo dõi học viên và dashboard

Hệ thống hỗ trợ các danh sách và số liệu cơ bản:

- Kỳ học phí còn tối đa 30 ngày và chưa có kỳ tiếp theo.
- Học sinh Active chưa từng có invoice, theo dõi trong danh sách chưa lập học phí riêng.
- Công nợ quá hạn.
- Học sinh đang học, bảo lưu hoặc đã nghỉ học.
- Học sinh vắng ít nhất 3 trong 10 session Completed gần nhất.

Dashboard chỉ hiển thị số liệu tổng hợp và danh sách cơ bản theo role, được truy vấn trực tiếp từ dữ liệu nghiệp vụ. Không xây lịch sử liên hệ, bảng thống kê riêng hoặc phân hệ thông báo; email, SMS và push notification nằm ngoài phạm vi.

## 9. Kiểm soát dữ liệu

- Dữ liệu tài chính, enrollment và attendance không bị xóa vật lý sau khi phát sinh.
- Đổi quyền, pause/resume và hủy tài chính phải có audit log.
- Dữ liệu trẻ em chỉ được người có quyền truy cập.
- AI không tự động thay đổi dữ liệu chính thức khi chưa được xác nhận.

## 10. Ngoài phạm vi core hiện tại

- Nhiều cơ sở, phòng học, trợ giảng.
- Tài khoản học sinh/phụ huynh.
- Học thử, học bù và quyền học theo số buổi.
- Thanh toán trực tuyến và thông báo đa kênh.
- Lịch sử liên hệ phụ huynh, import/export và báo cáo nâng cao.
- Chi tiết xử lý bên trong các module AI.

## 11. Công nghệ định hướng

- ReactJS.
- ASP.NET Core Web API / C#.
- SQL Server + Entity Framework Core.
- ASP.NET Core Identity + JWT, không refresh token.
- Docker.

## 12. Sổ quyết định nghiệp vụ

Mục này tổng hợp các quyết định để nhóm dễ kiểm tra khi thiết kế database, API và code. Các mục **cần chốt** chưa được xem là yêu cầu chính thức cho đến khi nhóm xác nhận.

### 12.1. Đã chốt

| Mã | Quyết định |
|---|---|
| B01 | Trung tâm có một cơ sở và hệ thống chỉ dành cho nhân viên nội bộ. |
| B02 | Mỗi tài khoản có một role chính; Teacher chỉ thao tác trên lớp phụ trách. |
| B03 | Học sinh và phụ huynh không có tài khoản. |
| B04 | Không hỗ trợ nghiệp vụ chuyển lớp. |
| B05 | Không lưu lớp hoặc trạng thái học trực tiếp trên Student; thông tin này được suy ra từ enrollment. |
| B06 | Mỗi học sinh có tối đa một enrollment `Active` hoặc `Paused` tại một thời điểm. |
| B07 | Enrollment có các trạng thái `Active`, `Paused`, `Completed`, `Withdrawn`; trạng thái kết thúc không chuyển ngược. |
| B08 | Bảo lưu giữ nguyên enrollment; bắt buộc có lý do, ngày dự kiến quay lại không bắt buộc; enrollment chỉ lưu thông tin lần bảo lưu hiện tại. |
| B09 | Nghỉ học chuyển enrollment thành `Withdrawn`, có ngày kết thúc và lý do; không xóa lịch sử đã phát sinh. |
| B10 | Chỉ lớp `Active` và còn chỗ mới nhận enrollment. |
| B11 | Lớp có một giáo viên chính; session có thể ghi nhận Teacher dạy thay; không quản lý trợ giảng. |
| B12 | Mỗi lớp có một lịch học cố định mỗi tuần; session được Admin tạo thủ công từ lịch này và thay lịch lớp không sửa các session đã tạo. |
| B13 | Attendance gồm `Present`, `ExcusedAbsent`, `UnexcusedAbsent`; không có bản ghi nghĩa là chưa điểm danh. |
| B14 | Session `Cancelled` không được điểm danh và không tính chuyên cần. |
| B15 | Học sinh vắng có phép hoặc không phép vẫn tính đủ học phí; attendance không làm thay đổi invoice. |
| B16 | Học phí thu theo kỳ cố định đúng 6 tháng, không tính theo số buổi học thực tế. |
| B17 | Một invoice có thể được thanh toán nhiều lần; chỉ payment `Confirmed` tính vào công nợ và doanh thu. |
| B18 | Không xóa chứng từ tài chính; hủy phải có lý do và audit. |
| B19 | Không hỗ trợ hoàn tiền, học bù, học thử hoặc quyền học theo số buổi trong core hiện tại. |
| B20 | Tái phí dựa trên kỳ invoice gần nhất của học sinh; vắng nhiều là vắng ít nhất 3 trong 10 session `Completed` gần nhất. |
| B21 | Một học sinh có thể có nhiều guardian, tối đa một người liên hệ chính và một người thanh toán chính. |
| B22 | Lịch sử liên hệ phụ huynh không thuộc phạm vi MVP. |
| B23 | Course, Level và Lesson đã được sử dụng không bị xóa, chỉ chuyển sang ngừng áp dụng; Lesson trực tiếp thuộc Level. |
| B24 | Dữ liệu enrollment, attendance và tài chính đã phát sinh không bị xóa vật lý. |
| B25 | AI không tự động thay đổi dữ liệu chính thức khi chưa được người có quyền xác nhận. |
| B26 | Enrollment `Paused` giữ chỗ và được tính vào sĩ số lớp. |
| B27 | Bảo lưu không tự động kéo dài kỳ học phí hoặc thay đổi invoice. |
| B28 | Không complete lớp khi còn enrollment `Active/Paused`; complete lớp không tự động complete enrollment. |
| B29 | Chỉ xóa enrollment nhập nhầm khi chưa có attendance, remark hoặc invoice. |
| B30 | Học sinh quay lại sau khi `Withdrawn` hoặc học level mới sau khi `Completed` phải tạo enrollment mới. |
| B31 | Chỉ enrollment `Active` được lập invoice mới. |
| B32 | Invoice phải có `AmountDue > 0` và `DueDate` không sau `PeriodEnd`. |
| B33 | Nghỉ học không tự động hủy invoice, payment hoặc công nợ đã phát sinh. |
| B34 | Học sinh phải có người liên hệ chính và người thanh toán chính trước khi ghi danh. |
| B35 | Không gỡ guardian đang giữ cờ chính khi chưa chọn người thay thế. |
| B36 | Không giảm Capacity thấp hơn tổng enrollment `Active/Paused`. |
| B37 | Attendance chỉ được sửa trước khi session `Completed`; hoàn tất session sẽ khóa điểm danh. |
| B38 | Không tạo nhận xét mới cho enrollment `Completed/Withdrawn`. |
| B39 | Hoàn thành enrollment chỉ bắt buộc ngày kết thúc, không bắt buộc lý do. |
| B40 | Học sinh `Active` chưa từng có invoice được theo dõi riêng, không xếp vào danh sách tái phí. |
| B41 | Chỉ chuyển session sang `Completed` khi mọi enrollment hợp lệ đã được điểm danh; sau đó session và attendance bị khóa, không mở lại trong MVP. |
| B42 | Enrollment hợp lệ để điểm danh phải thuộc cùng lớp, có trạng thái `Active` và khoảng ngày enrollment bao phủ `SessionDate`; không điểm danh enrollment `Paused/Completed/Withdrawn`. |

### 12.2. Cần chốt

