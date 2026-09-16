using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Invoices;

/// <summary>
/// DTO yêu cầu tạo mới hóa đơn học phí cho một enrollment đang Active.
/// PeriodEnd được hệ thống tự tính: PeriodStart + 6 tháng - 1 ngày.
/// </summary>
public sealed record YeuCauTaoHoaDon(
    int EnrollmentId,
    DateOnly PeriodStart,
    decimal AmountDue,
    DateOnly DueDate,
    string? Note);

/// <summary>
/// DTO yêu cầu hủy hóa đơn. Bắt buộc có lý do để lưu audit log.
/// </summary>
public sealed record YeuCauHuyHoaDon(
    string LyDoHuy);

/// <summary>
/// DTO phản hồi chi tiết hóa đơn học phí.
/// </summary>
public sealed record PhanHoiHoaDon(
    int Id,
    string SoHoaDon,
    int EnrollmentId,
    int StudentId,
    string TenHocVien,
    string MaHocVien,
    DateOnly NgayBatDauKy,
    DateOnly NgayKetThucKy,
    decimal SoTienPhaiTra,
    DateOnly NgayDenHan,
    InvoiceStatus TrangThai,
    decimal TongDaXacNhan,
    decimal ConNo,
    string? GhiChu,
    string NguoiTao,
    DateTime NgayTao,
    string? NguoiHuy,
    DateTime? NgayHuy,
    string? LyDoHuy);

/// <summary>
/// DTO tổng hợp công nợ của một học sinh.
/// </summary>
public sealed record PhanHoiCongNo(
    int StudentId,
    string TenHocVien,
    string MaHocVien,
    decimal TongPhaiTra,
    decimal TongDaXacNhan,
    decimal ConNo,
    bool CoHoaDonQuaHan,
    IReadOnlyList<PhanHoiHoaDon> DanhSachHoaDon);

/// <summary>
/// DTO tóm tắt học sinh còn nợ dùng cho dashboard.
/// </summary>
public sealed record PhanHoiHocVienConNo(
    int StudentId,
    string TenHocVien,
    string MaHocVien,
    decimal ConNo,
    bool CoHoaDonQuaHan,
    int SoHoaDonChuaTra);
