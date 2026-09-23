namespace CmsEdu.Application.Dashboard;

public record PhanHoiDashboardAdmin(
    int SoNhanVienDangHoatDong,
    int SoHocVienDangHoatDong,
    int SoLopDangHoatDong,
    int SoGhiDanhDangHoc);

public record PhanHoiBuoiHocSapToi(
    int Id,
    int LopId,
    string MaLop,
    string TenLop,
    DateOnly NgayHoc,
    TimeOnly GioBatDau,
    TimeOnly GioKetThuc);

public record PhanHoiDashboardTeacher(
    int SoLopDangPhuTrach,
    int SoHocVienDangHoc,
    int SoBuoiHocHomNay,
    IReadOnlyList<PhanHoiBuoiHocSapToi> BuoiHocSapToi);

public record PhanHoiDashboardCustomerCare(
    int SoHocVienDangHoatDong,
    int SoGhiDanhDangHoc,
    int SoGhiDanhBaoLuu,
    int SoHocVienChuaCoNguoiGiamHo);

public record PhanHoiGiaoDichTaiChinh(
    int PaymentId,
    string PaymentNumber,
    string ReceiptNumber,
    int InvoiceId,
    string InvoiceNumber,
    int StudentId,
    string TenHocVien,
    decimal Amount,
    DateTimeOffset PaidAt,
    string Method,
    string Status,
    string? Note);

public record PhanHoiAuditTaiChinh(
    int Id,
    string? UserId,
    string Action,
    string EntityType,
    string EntityId,
    string Description,
    DateTimeOffset OccurredAt);

public record PhanHoiDashboardTaiChinh(
    decimal Revenue,
    decimal Debt,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    IReadOnlyList<PhanHoiGiaoDichTaiChinh> Transactions,
    IReadOnlyList<PhanHoiAuditTaiChinh> AuditLogs);
