namespace CmsEdu.Application.Dashboard;

/// <summary>Giao dịch payment hiển thị trên dashboard tài chính.</summary>
public sealed record PhanHoiGiaoDichTaiChinh(
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

/// <summary>Audit liên quan đến invoice và payment trên dashboard tài chính.</summary>
public sealed record PhanHoiAuditTaiChinh(
    int Id,
    string? UserId,
    string Action,
    string EntityType,
    string EntityId,
    string Description,
    DateTimeOffset OccurredAt);

/// <summary>Số liệu tổng hợp và dữ liệu giao dịch cho dashboard tài chính dùng chung.</summary>
public sealed record PhanHoiDashboardTaiChinh(
    decimal Revenue,
    decimal Debt,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    IReadOnlyList<PhanHoiGiaoDichTaiChinh> Transactions,
    IReadOnlyList<PhanHoiAuditTaiChinh> AuditLogs);
