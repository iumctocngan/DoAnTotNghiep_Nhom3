using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Payments;

/// <summary>Yêu cầu ghi nhận một khoản thanh toán cho hóa đơn.</summary>
public sealed record YeuCauTaoPayment(
    decimal Amount,
    DateTimeOffset PaidAt,
    PaymentMethod Method,
    string? Note);

/// <summary>Yêu cầu hủy một khoản thanh toán.</summary>
public sealed record YeuCauHuyPayment(string LyDoHuy);

/// <summary>Thông tin khoản thanh toán và phiếu thu.</summary>
public sealed record PhanHoiPayment(
    int Id,
    string PaymentNumber,
    string ReceiptNumber,
    int InvoiceId,
    string InvoiceNumber,
    int StudentId,
    string TenHocVien,
    decimal Amount,
    DateTimeOffset PaidAt,
    PaymentMethod Method,
    PaymentStatus Status,
    string? Note,
    string CreatedBy,
    string? CancelledBy,
    DateTimeOffset? CancelledAt,
    string? CancelReason);
