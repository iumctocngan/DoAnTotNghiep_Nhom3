# Thiết kế database Core Platform — CMS EDU

Thiết kế MVP dựa trên [overview.md](overview.md), gồm 14 bảng nghiệp vụ, một bảng refresh token phục vụ xác thực và các bảng ASP.NET Core Identity.

## 1. Quy ước

- Khóa chính bảng nghiệp vụ: `int IDENTITY(1,1)`; FK tương ứng dùng `int`.
- Khóa của Identity giữ kiểu mặc định để tránh tùy biến không cần thiết.
- Ngày dùng `date`, giờ dùng `time`, mốc thời gian dùng `datetime2` UTC.
- Tiền VND dùng `decimal(18,0)`.
- Trạng thái dùng enum trong code và số nguyên nhỏ trong database.
- Bảng danh mục dùng `IsActive`; dữ liệu đã phát sinh không xóa vật lý.
- `CreatedAt/By`, `UpdatedAt/By` chỉ thêm vào bảng cần truy vết.
- Dấu `?` sau tên cột nghĩa là cột có thể để trống (`NULL`).

## 2. Tài khoản nhân viên

Sử dụng các bảng Identity chuẩn: `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles` và bảng phụ trợ.

Mở rộng `ApplicationUser`:

| Cột | Ý nghĩa |
|---|---|
| EmployeeCode | Mã nhân viên do Admin nhập, unique và không được sửa hoặc tái sử dụng |
| FullName | Họ tên |
| PhoneNumber? | Số điện thoại không bắt buộc, không unique |
| EmploymentStatus | Active, Inactive |

Role cố định: `Admin`, `Teacher`, `Accountant`, `CustomerCare`. Application layer chỉ cho mỗi user một role chính; Admin không được tự đổi role hoặc tự deactivate; hệ thống luôn phải còn ít nhất một Admin Active; không cho deactivate/đổi role Teacher khi user còn phụ trách lớp Active. Admin được đổi role của tài khoản Inactive. Tài khoản mới luôn là Active, có EmailConfirmed bằng true và giữ nguyên role khi deactivate.

Tài khoản Inactive không được đăng nhập hoặc sử dụng refresh token để cấp access token mới. Admin có thể reset mật khẩu khi tài khoản đang Inactive; thao tác này không tự activate tài khoản. Khi activate, tài khoản giữ nguyên role. Activate/deactivate lặp lại không thay đổi dữ liệu và không tạo AuditLog mới. Email là tên đăng nhập, được phép thay đổi nếu không trùng và phải đồng bộ với UserName. Admin được sửa hồ sơ của chính mình nhưng không được tự đổi role hoặc tự deactivate. Nhân viên Active được tự đổi mật khẩu bằng mật khẩu hiện tại; đổi thành công thu hồi toàn bộ refresh token và yêu cầu đăng nhập lại.

### RefreshTokens (Phiên đăng nhập)

```text
Id, UserId, TokenHash (SHA-256, unique), FamilyId,
CreatedAt, ExpiresAt, CreatedByIp?, RevokedAt?, RevokedByIp?,
ReplacedByTokenHash?, RevokeReason?
```

Quy tắc:

- Chỉ lưu hash của refresh token; token thô chỉ được trả cho client khi phát hành.
- Mỗi lần refresh sẽ thu hồi token cũ và tạo token mới trong cùng `FamilyId`.
- Dùng lại token đã bị thay thế sẽ thu hồi toàn bộ token trong cùng family.
- Logout thu hồi token của phiên hiện tại; logout-all, deactivate, đổi role và reset password thu hồi toàn bộ refresh token của user.
- Refresh token hết hạn hoặc đã thu hồi không được sử dụng.
- Access token mặc định hết hạn sau 15 phút; refresh token mặc định hết hạn sau 7 ngày.
- Refresh token là dữ liệu xác thực, không được tính vào 14 bảng nghiệp vụ của core.

Tài khoản Admin khởi tạo được seed trong môi trường Development khi có `SeedAdmin:Email` và tài khoản đó chưa tồn tại. Mật khẩu, họ tên và mã nhân viên được cung cấp qua cấu hình bảo mật hoặc các biến môi trường `SeedAdmin__Password`, `SeedAdmin__FullName`, `SeedAdmin__EmployeeCode`; không lưu mật khẩu mặc định trong source code. Khi Admin tạo nhân viên, Admin nhập EmployeeCode và mật khẩu tạm. Danh sách quản trị mặc định gồm cả Active và Inactive. Tạo tài khoản, đổi role, activate, deactivate và reset mật khẩu đều ghi AuditLog nhưng không ghi nội dung mật khẩu.

## 3. Học sinh và phụ huynh

### Students (Học sinh)

```text
Id (khóa chính), StudentCode (mã học sinh, unique),
FullName (họ tên), DateOfBirth (ngày sinh), Gender? (giới tính),
LearningNote? (lưu ý học tập), IsArchived (đã lưu trữ hồ sơ)
```

Không lưu `CurrentClassId` hoặc trạng thái học; dữ liệu này được suy ra từ enrollment.

### Guardians (Người giám hộ)

```text
Id (khóa chính), FullName (họ tên), Phone (số điện thoại),
Email? (email), IsActive (còn sử dụng để liên hệ)
```

`IsActive` cho biết thông tin guardian còn được sử dụng để liên hệ. Quan hệ guardian với từng học sinh được quản lý riêng trong `StudentGuardians`.

### StudentGuardians (Quan hệ học sinh - người giám hộ)

```text
StudentId (học sinh), GuardianId (người giám hộ),
Relationship (mối quan hệ), IsPrimary (người giám hộ chính)
```

Ràng buộc:

- Unique `(StudentId, GuardianId)`.
- Mỗi học sinh có tối đa một guardian chính, được bảo vệ bằng unique filtered index theo `StudentId` khi `IsPrimary = 1`; guardian này là đầu mối liên hệ và đóng học phí.
- Trước khi ghi danh, học sinh phải có một guardian chính.
- Không gỡ liên kết đang giữ cờ chính khi chưa chọn người thay thế; thao tác đổi guardian chính dùng transaction.

## 4. Chương trình học

### Courses (Chương trình học)

```text
Id (khóa chính), Code (mã chương trình, unique), Name (tên),
MinAge? (tuổi tối thiểu), MaxAge? (tuổi tối đa),
Description? (mô tả), IsActive (đang áp dụng)
```

### Levels (Cấp độ)

```text
Id (khóa chính), CourseId (chương trình), Code (mã cấp độ),
Name (tên), SortOrder (thứ tự), IsActive (đang áp dụng)
```

Unique `(CourseId, Code)` và `(CourseId, SortOrder)`.

### Lessons (Bài học)

```text
Id (khóa chính), LevelId (cấp độ), Code (mã bài học), Name (tên),
Objective? (mục tiêu), SortOrder (thứ tự), IsActive (đang áp dụng)
```

Unique `(LevelId, Code)` và `(LevelId, SortOrder)`.

Lesson trực tiếp thuộc Level.

## 5. Lớp và ghi danh

### Classes (Lớp học)

```text
Id (khóa chính), ClassCode (mã lớp, unique), Name (tên lớp),
LevelId (cấp độ), MainTeacherUserId (giáo viên chính),
Capacity (sĩ số tối đa), StartDate (ngày bắt đầu),
EndDate? (ngày kết thúc), DayOfWeek (thứ học hằng tuần),
StartTime (giờ bắt đầu), EndTime (giờ kết thúc), Status (trạng thái)
```

`Status`: Preparing, Active, Completed, Cancelled. Giáo viên chính phải có role Teacher.

Không giảm `Capacity` thấp hơn tổng enrollment `Active` và `Paused`; không complete lớp khi còn một trong hai trạng thái này.

Mỗi lớp có một lịch học cố định mỗi tuần. Session đã tạo giữ ngày giờ thực tế nên thay lịch lớp không làm thay đổi session cũ. Check `StartTime < EndTime` và kiểm tra trùng lịch giáo viên ở application layer.

### Enrollments (Ghi danh/xếp lớp)

```text
Id (khóa chính), StudentId (học sinh), ClassId (lớp học),
StartDate (ngày bắt đầu), EndDate? (ngày kết thúc), Status (trạng thái),
EndReason? (lý do kết thúc), PauseReason? (lý do bảo lưu),
ExpectedReturnDate? (ngày dự kiến học lại)
```

`Status`: Active, Paused, Completed, Withdrawn.

Quy tắc:

- `StartDate <= EndDate` nếu có EndDate.
- Mỗi học sinh có tối đa một enrollment mang trạng thái `Active` hoặc `Paused`.
- Tổng enrollment `Active` và `Paused` không vượt Capacity.
- Pause bắt buộc có `PauseReason`; `ExpectedReturnDate` không bắt buộc và nếu có phải sau ngày thực hiện bảo lưu.
- Pause/resume không tự động thay đổi invoice.
- Enrollment chỉ lưu thông tin lần bảo lưu hiện tại; chưa audit pause/resume trong MVP.
- Không xóa enrollment. Trường hợp nhập nhầm chuyển `Withdrawn` và ghi lý do.
- Sau khi `Completed` hoặc `Withdrawn`, nếu học sinh tiếp tục học thì tạo enrollment mới.
- Complete chỉ bắt buộc `EndDate`; withdraw bắt buộc `EndDate` và `EndReason`.

## 6. Session, điểm danh và nhận xét

### Sessions (Buổi học)

```text
Id (khóa chính), ClassId (lớp học), LessonId? (bài đã dạy),
SessionDate (ngày học),
StartTime (giờ bắt đầu), EndTime (giờ kết thúc),
Status (trạng thái), Note? (ghi chú)
```

`Status`: Scheduled, Completed, Cancelled. Session Scheduled có thể đổi ngày giờ; chỉ chuyển sang Completed khi mọi enrollment hợp lệ tại SessionDate đã có attendance. Session đã hoàn thành không được sửa, hủy hoặc mở lại trong MVP.

`LessonId` nếu có phải trỏ tới lesson thuộc cùng level với lớp của session.

Giáo viên của session được suy ra từ `Classes.MainTeacherUserId`; chưa hỗ trợ giáo viên dạy thay trong MVP.

### Attendances (Điểm danh)

```text
Id (khóa chính), SessionId (buổi học), EnrollmentId (ghi danh),
Status (trạng thái điểm danh), Note? (ghi chú),
MarkedBy (người điểm danh), MarkedAt (thời điểm điểm danh),
UpdatedBy? (người sửa), UpdatedAt? (thời điểm sửa)
```

`Status`: Present, Absent. Lý do vắng nếu cần được lưu trong `Note`.

Ràng buộc:

- Unique `(SessionId, EnrollmentId)`.
- Enrollment hợp lệ để điểm danh phải thuộc cùng lớp, có `Status = Active`, `StartDate <= SessionDate` và (`EndDate IS NULL` hoặc `EndDate >= SessionDate`). Enrollment Paused, Completed hoặc Withdrawn không được tạo attendance.
- Không điểm danh session Cancelled.
- Không có bản ghi nghĩa là chưa điểm danh.
- Admin hoặc Teacher phụ trách được sửa attendance trước khi session Completed; hoàn tất session sẽ khóa điểm danh.
- Trước khi hoàn tất session, số attendance phải bao phủ toàn bộ enrollment hợp lệ tại SessionDate.

### StudentRemarks (Nhận xét học sinh)

```text
Id (khóa chính), EnrollmentId (ghi danh),
SessionId? (buổi học, có thể để trống), Content (nội dung),
CreatedBy (người tạo), CreatedAt (thời điểm tạo),
UpdatedBy? (người sửa), UpdatedAt? (thời điểm sửa)
```

`SessionId` để trống khi nhận xét cho cả giai đoạn.

Nếu có `SessionId`, session phải thuộc cùng lớp với enrollment và nằm trong thời gian enrollment có hiệu lực.

Không tạo remark mới cho enrollment `Completed` hoặc `Withdrawn`.

## 7. Học phí

### Invoices (Khoản phải thu/kỳ học phí)

```text
Id (khóa chính), InvoiceNumber (mã invoice, unique),
EnrollmentId (ghi danh), PeriodStart (đầu kỳ), PeriodEnd (cuối kỳ),
AmountDue (số tiền phải thu), DueDate (hạn thanh toán),
Status (trạng thái), Note? (ghi chú), CreatedBy (người tạo),
CreatedAt (thời điểm tạo), CancelledBy? (người hủy),
CancelledAt? (thời điểm hủy), CancelReason? (lý do hủy)
```

`Status`: Issued, Cancelled.

Quy tắc:

- Chỉ enrollment `Active` được lập invoice mới.
- `AmountDue > 0`.
- `PeriodEnd = PeriodStart + 6 tháng - 1 ngày`.
- `DueDate <= PeriodEnd`.
- Kỳ invoice còn hiệu lực của cùng học sinh không được chồng lấn, kể cả qua enrollment khác.
- Không hủy invoice khi còn payment Confirmed.
- Hủy invoice bắt buộc có lý do; cập nhật trạng thái, người hủy, thời gian hủy và tạo AuditLog trong cùng transaction.
- Withdraw enrollment không tự động hủy invoice, payment hoặc công nợ đã phát sinh.

### Payments (Khoản thanh toán/phiếu thu)

```text
Id (khóa chính), PaymentNumber (mã thanh toán, unique),
ReceiptNumber (số phiếu thu, unique), InvoiceId (invoice),
Amount (số tiền thu), PaidAt (thời điểm thu), Method (phương thức),
Status (trạng thái), Note? (ghi chú), CreatedBy (người tạo),
CancelledBy? (người hủy), CancelledAt? (thời điểm hủy),
CancelReason? (lý do hủy)
```

`Method`: Cash, BankTransfer. `Status`: Confirmed, Cancelled.

Quy tắc:

- `Amount > 0`.
- Tổng payment Confirmed không vượt AmountDue.
- Chỉ payment Confirmed tính vào công nợ và doanh thu.
- Không xóa payment; hủy bắt buộc có lý do và phải cập nhật trạng thái, người hủy, thời gian hủy, đồng thời tạo AuditLog trong cùng transaction.
- Không cần bảng Receipt riêng vì một payment có một ReceiptNumber.

### Dữ liệu tính động

```text
PaidAmount (đã thu) = SUM(Payment Confirmed)
DebtAmount (công nợ) = AmountDue - PaidAmount
PaymentStatus (tình trạng thanh toán) = Unpaid / PartiallyPaid / Paid / Overdue
Revenue (doanh thu) = SUM(Payment Confirmed theo PaidAt)
MissingInvoice (chưa lập học phí) = học sinh có enrollment Active nhưng chưa từng có invoice
```

## 8. Dữ liệu theo dõi tính động

Không tạo bảng riêng cho chưa lập học phí, công nợ quá hạn, dashboard hoặc notification. Các danh sách MVP được truy vấn trực tiếp từ dữ liệu nghiệp vụ. Cảnh báo tái phí 30 ngày và thống kê vắng nhiều được hoãn sau MVP.

## 9. Audit

### AuditLogs (Nhật ký thay đổi)

```text
Id (khóa chính), UserId? (người thực hiện), Action (hành động),
EntityType (loại dữ liệu), EntityId (ID dữ liệu),
Description (mô tả), OccurredAt (thời điểm xảy ra)
```

Service chủ động tạo mô tả ngắn cho thao tác cần audit. MVP audit các thao tác: đổi quyền, hủy invoice và hủy payment. Không audit mọi truy vấn đọc; audit pause/resume được hoãn.

## 10. Danh sách 14 bảng nghiệp vụ

```text
Students              Guardians             StudentGuardians
Courses               Levels                Lessons
Classes               Enrollments            Sessions
Attendances            StudentRemarks        Invoices
Payments               AuditLogs
```

Bảng `RefreshTokens` thuộc hạ tầng xác thực và nằm ngoài danh sách 14 bảng nghiệp vụ trên.

## 11. Quy tắc xử lý ở application layer

1. Kiểm tra role giáo viên chính và trùng lịch lớp/session; session sử dụng giáo viên chính của lớp.
2. Kiểm tra tổng enrollment `Active/Paused` không vượt sức chứa và mỗi học sinh có tối đa một enrollment ở hai trạng thái này.
3. Kiểm tra enrollment hợp lệ khi điểm danh; chỉ complete session khi tất cả enrollment hợp lệ đã có attendance; không sửa session hoặc attendance sau khi Completed; lesson được ghi nhận trong session phải thuộc cùng level với lớp; session của nhận xét phải phù hợp với lớp.
4. Kiểm tra kỳ invoice 6 tháng không chồng lấn theo học sinh.
5. Kiểm tra tổng payment không vượt AmountDue.
6. Cập nhật guardian chính phải dùng transaction.
7. Kiểm tra `Capacity > 0`, khoảng ngày hợp lệ, `StartTime < EndTime` và `MinAge <= MaxAge`.
8. Kiểm tra học sinh có guardian chính trước khi ghi danh.
9. Kiểm tra trạng thái enrollment khi pause/resume, tạo invoice, complete, withdraw và tạo remark.
10. Xoay refresh token trong transaction, chỉ lưu token hash và thu hồi token family khi phát hiện token cũ bị tái sử dụng.

## 12. Ranh giới và artifact báo cáo

- Chưa có schema file, CLO, rubric hoặc dữ liệu nội bộ AI.
- Draft AI thuộc module AI; nội dung được duyệt sẽ thiết kế khi tích hợp.
- ERD được sinh từ migration thực tế sau khi entity ổn định để đưa vào báo cáo.
