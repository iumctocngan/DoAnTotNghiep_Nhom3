using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Invoices;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Common.Interfaces;

/// <summary>
/// Giao diện dịch vụ quản lý nghiệp vụ hóa đơn học phí (Invoice).
/// </summary>
public interface IDichVuHoaDon
{
    /// <summary>
    /// Lấy danh sách hóa đơn có thể lọc theo enrollment, học sinh và trạng thái.
    /// </summary>
    Task<PagedResult<PhanHoiHoaDon>> LayDanhSachHoaDonAsync(
        int? enrollmentId,
        int? studentId,
        InvoiceStatus? trangThai,
        int trang,
        int kichThuocTrang,
        CancellationToken maHuy = default);

    /// <summary>
    /// Lấy chi tiết hóa đơn theo mã định danh.
    /// </summary>
    Task<PhanHoiHoaDon> LayChiTietHoaDonAsync(
        int maHoaDon,
        CancellationToken maHuy = default);

    /// <summary>
    /// Tạo hóa đơn mới cho enrollment đang Active.
    /// Ràng buộc: chỉ enrollment Active, kỳ không được chồng lấn, AmountDue > 0, DueDate &lt;= PeriodEnd.
    /// </summary>
    Task<PhanHoiHoaDon> TaoHoaDonAsync(
        YeuCauTaoHoaDon yeuCau,
        CancellationToken maHuy = default);

    /// <summary>
    /// Hủy hóa đơn. Không xóa vật lý — bắt buộc có lý do và ghi audit log.
    /// </summary>
    Task<PhanHoiHoaDon> HuyHoaDonAsync(
        int maHoaDon,
        YeuCauHuyHoaDon yeuCau,
        CancellationToken maHuy = default);

    /// <summary>
    /// Lấy danh sách hóa đơn của một học sinh, sắp xếp theo kỳ mới nhất.
    /// </summary>
    Task<PagedResult<PhanHoiHoaDon>> LayDanhSachHoaDonTheoHocVienAsync(
        int studentId,
        int trang,
        int kichThuocTrang,
        CancellationToken maHuy = default);

    /// <summary>
    /// Tính tổng công nợ hiện tại của học sinh.
    /// Công thức: Debt = Σ AmountDue (chưa hủy) - Σ Amount (Confirmed).
    /// </summary>
    Task<PhanHoiCongNo> TinhCongNoHocVienAsync(
        int studentId,
        CancellationToken maHuy = default);

    /// <summary>
    /// Lấy danh sách học sinh còn công nợ chưa thanh toán (dùng cho dashboard).
    /// </summary>
    Task<PagedResult<PhanHoiHocVienConNo>> LayDanhSachHocVienConNoAsync(
        int trang,
        int kichThuocTrang,
        CancellationToken maHuy = default);
}
