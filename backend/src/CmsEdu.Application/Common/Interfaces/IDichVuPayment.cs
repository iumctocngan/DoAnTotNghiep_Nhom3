using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Payments;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Common.Interfaces;

/// <summary>Giao diện xử lý nghiệp vụ khoản thanh toán và phiếu thu.</summary>
public interface IDichVuPayment
{
    /// <summary>Lấy danh sách khoản thanh toán theo các bộ lọc nghiệp vụ.</summary>
    Task<PagedResult<PhanHoiPayment>> LayDanhSachPaymentAsync(
        int? invoiceId,
        int? studentId,
        PaymentStatus? status,
        PaymentMethod? method,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Lấy chi tiết một khoản thanh toán.</summary>
    Task<PhanHoiPayment> LayChiTietPaymentAsync(
        int paymentId,
        CancellationToken cancellationToken = default);

    /// <summary>Ghi nhận khoản thanh toán và cập nhật trạng thái hóa đơn.</summary>
    Task<PhanHoiPayment> TaoPaymentAsync(
        int invoiceId,
        YeuCauTaoPayment request,
        CancellationToken cancellationToken = default);

    /// <summary>Hủy khoản thanh toán và cập nhật trạng thái hóa đơn.</summary>
    Task<PhanHoiPayment> HuyPaymentAsync(
        int paymentId,
        YeuCauHuyPayment request,
        CancellationToken cancellationToken = default);
}
