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
→ Lập kỳ học phí tiếp theo / bảo lưu / kết thúc
```

Quy tắc:

- Enrollment lưu lý do và ngày dự kiến quay lại của lần bảo lưu hiện tại. Bảo lưu không tự động thay đổi invoice.
- Hoàn thành phải lưu ngày kết thúc; nghỉ học phải lưu ngày kết thúc và lý do.
- Học sinh quay lại sau khi nghỉ hoặc học level mới sau khi hoàn thành phải tạo enrollment mới.
- Không xóa enrollment; trường hợp nhập nhầm chuyển sang `Withdrawn` và ghi rõ lý do.

## 5. Quản lý con người

### Học sinh và phụ huynh

- Lưu mã học sinh, họ tên, ngày sinh và lưu ý học tập.
- Một học sinh có thể có nhiều người giám hộ.
- Xác định một người giám hộ chính; người này mặc định là đầu mối liên hệ và đóng học phí.
- Phải có người giám hộ chính trước khi ghi danh.

### Giáo viên

- Lưu hồ sơ và trạng thái làm việc.
- Mỗi lớp có một giáo viên chính.
- Session sử dụng giáo viên chính của lớp; chưa quản lý giáo viên dạy thay trong MVP.
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
- Trạng thái: Present hoặc Absent; lý do vắng nếu cần được ghi trong Note.
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
- Kỳ học phí tiếp theo được lập thủ công và không được chồng lấn với kỳ đã có.
- Nghỉ học không tự động hủy invoice, payment hoặc công nợ đã phát sinh.
- Điểm danh không làm thay đổi học phí.

## 8. Theo dõi học viên và dashboard

Hệ thống hỗ trợ các danh sách và số liệu cơ bản:

- Học sinh Active chưa từng có invoice, theo dõi trong danh sách chưa lập học phí riêng.
- Công nợ quá hạn.
- Học sinh đang học, bảo lưu hoặc đã nghỉ học.
- Lớp và session sắp diễn ra theo phạm vi role.

Dashboard chỉ hiển thị số liệu tổng hợp và danh sách cơ bản theo role, được truy vấn trực tiếp từ dữ liệu nghiệp vụ. Không xây lịch sử liên hệ, bảng thống kê riêng hoặc phân hệ thông báo; email, SMS và push notification nằm ngoài phạm vi.

## 9. Kiểm soát dữ liệu

- Dữ liệu tài chính, enrollment và attendance không bị xóa vật lý sau khi phát sinh.
- Đổi quyền và hủy chứng từ tài chính phải có audit log.
- Dữ liệu trẻ em chỉ được người có quyền truy cập.
- AI không tự động thay đổi dữ liệu chính thức khi chưa được xác nhận.

## 10. Ngoài phạm vi core hiện tại

- Nhiều cơ sở, phòng học, trợ giảng.
- Tài khoản học sinh/phụ huynh.
- Học thử, học bù và quyền học theo số buổi.
- Thanh toán trực tuyến và thông báo đa kênh.
- Lịch sử liên hệ phụ huynh, import/export và báo cáo nâng cao.
- Cảnh báo tái phí 30 ngày và phát hiện vắng nhiều theo 10 session gần nhất.
- Giáo viên dạy thay và phân biệt vắng có phép/không phép.
- Chi tiết xử lý bên trong các module AI.

## 11. Công nghệ định hướng

- ReactJS.
- ASP.NET Core Web API / C#.
- SQL Server + Entity Framework Core.
- ASP.NET Core Identity + JWT access token và refresh token có rotation.
- Docker.

## 12. Sổ quyết định nghiệp vụ

Mục này tổng hợp các quyết định MVP để nhóm đối chiếu khi thiết kế database, API và code. Các tính năng hoãn được ghi riêng để không vô tình đưa trở lại phạm vi triển khai ban đầu.

### 12.1. Đã chốt

| Mã | Quyết định |
|---|---|
| B01 | Trung tâm có một cơ sở, hệ thống chỉ dành cho nhân viên nội bộ và mỗi tài khoản có một role chính. |
| B02 | Học sinh và guardian không có tài khoản; Teacher chỉ thao tác trên lớp mình phụ trách. |
| B03 | Course → Level → Lesson; danh mục đã sử dụng không bị xóa, chỉ ngừng áp dụng. |
| B04 | Một học sinh có thể có nhiều guardian nhưng chỉ có một guardian chính; phải có guardian chính trước khi ghi danh. |
| B05 | Không lưu lớp hoặc trạng thái học trên Student; dữ liệu này được suy ra từ Enrollment. |
| B06 | Mỗi học sinh có tối đa một Enrollment `Active/Paused`; `Paused` giữ chỗ và tính vào sĩ số. |
| B07 | Enrollment chuyển `Active ↔ Paused`, `Active → Completed`, `Active/Paused → Withdrawn`; trạng thái kết thúc không chuyển ngược. |
| B08 | Pause bắt buộc lý do; Complete bắt buộc ngày kết thúc; Withdraw bắt buộc ngày kết thúc và lý do. Không xóa Enrollment, kể cả nhập nhầm. |
| B09 | Chỉ lớp `Active` và còn chỗ mới nhận Enrollment; không giảm Capacity dưới sĩ số `Active/Paused` và không complete lớp khi còn các Enrollment này. |
| B10 | Mỗi lớp có một giáo viên chính và một lịch cố định mỗi tuần; Session dùng giáo viên chính, được Admin tạo thủ công và giữ ngày giờ riêng. |
| B11 | Session chuyển từ `Scheduled` sang `Completed/Cancelled`; Session `Cancelled` không được điểm danh. |
| B12 | Attendance gồm `Present/Absent`; chỉ Enrollment `Active` thuộc cùng lớp và có hiệu lực tại `SessionDate` được điểm danh. Phải điểm danh đủ trước khi complete Session và sau đó dữ liệu bị khóa. |
| B13 | Remark chỉ được tạo cho Enrollment đang `Active/Paused`; nếu gắn Session thì Session phải thuộc cùng lớp và thời gian Enrollment. |
| B14 | Học phí theo kỳ đúng 6 tháng; chỉ Enrollment `Active` được lập Invoice; kỳ không chồng lấn, `AmountDue > 0` và `DueDate <= PeriodEnd`. |
| B15 | Một Invoice có nhiều Payment; chỉ Payment `Confirmed` tính công nợ/doanh thu. Không xóa chứng từ; hủy bắt buộc lý do và audit. Attendance, pause hoặc withdraw không tự thay đổi học phí. |
| B16 | Dashboard MVP chỉ gồm số liệu cơ bản, danh sách chưa lập học phí và công nợ quá hạn; cảnh báo tái phí 30 ngày và vắng nhiều được hoãn. AI không tự thay đổi dữ liệu chính thức nếu chưa được xác nhận. |
| B17 | Access token có thời hạn ngắn; refresh token được lưu dạng hash, xoay vòng sau mỗi lần sử dụng và có thể thu hồi theo từng phiên hoặc toàn bộ tài khoản. |

### 12.2. Hoãn sau MVP

- Giáo viên dạy thay và trợ giảng.
- Phân biệt vắng có phép/không phép.
- Cảnh báo tái phí trong 30 ngày.
- Phát hiện học sinh vắng ít nhất 3 trong 10 session gần nhất.
- Audit pause/resume, lịch sử liên hệ và báo cáo nâng cao.
