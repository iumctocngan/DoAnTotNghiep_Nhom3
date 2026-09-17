using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Invoices;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

/// <summary>
/// Cung cấp API quản lý hóa đơn học phí: xem, tạo, hủy và theo dõi công nợ.
/// </summary>
[ApiController]
[Route("api/invoices")]
[Authorize(Roles = $"{UserRole.Admin},{UserRole.Accountant}")]
public class HoaDonController(IDichVuHoaDon dichVuHoaDon) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách hóa đơn, có thể lọc theo ghi danh, học sinh và trạng thái.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PhanHoiHoaDon>>> LayDanhSachHoaDon(
        [FromQuery(Name = "enrollmentId")] int? maGhiDanh,
        [FromQuery(Name = "studentId")]    int? maHocVien,
        [FromQuery(Name = "status")]       InvoiceStatus? trangThai,
        [FromQuery(Name = "page")]         int trang = 1,
        [FromQuery(Name = "pageSize")]     int kichThuocTrang = 20,
        CancellationToken maHuy = default)
    {
        return Ok(await dichVuHoaDon.LayDanhSachHoaDonAsync(
            maGhiDanh, maHocVien, trangThai, trang, kichThuocTrang, maHuy));
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một hóa đơn theo mã định danh.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PhanHoiHoaDon>> LayChiTietHoaDon(
        [FromRoute(Name = "id")] int maHoaDon,
        CancellationToken maHuy = default)
    {
        return Ok(await dichVuHoaDon.LayChiTietHoaDonAsync(maHoaDon, maHuy));
    }

    /// <summary>
    /// Tạo hóa đơn mới cho enrollment đang Active.
    /// Ràng buộc: kỳ không được chồng lấn, DueDate &lt;= PeriodEnd, AmountDue &gt; 0.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PhanHoiHoaDon>> TaoHoaDon(
        [FromBody] YeuCauTaoHoaDon yeuCau,
        CancellationToken maHuy = default)
    {
        var hoaDon = await dichVuHoaDon.TaoHoaDonAsync(yeuCau, maHuy);
        return CreatedAtAction(nameof(LayChiTietHoaDon), new { id = hoaDon.Id }, hoaDon);
    }

    /// <summary>
    /// Hủy hóa đơn. Bắt buộc có lý do — không xóa vật lý, lưu audit log.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<PhanHoiHoaDon>> HuyHoaDon(
        [FromRoute(Name = "id")] int maHoaDon,
        [FromBody] YeuCauHuyHoaDon yeuCau,
        CancellationToken maHuy = default)
    {
        return Ok(await dichVuHoaDon.HuyHoaDonAsync(maHoaDon, yeuCau, maHuy));
    }

    /// <summary>
    /// Lấy danh sách hóa đơn của một học sinh cụ thể, sắp xếp theo kỳ mới nhất.
    /// </summary>
    [HttpGet("students/{studentId:int}")]
    public async Task<ActionResult<PagedResult<PhanHoiHoaDon>>> LayDanhSachHoaDonTheoHocVien(
        [FromRoute(Name = "studentId")] int maHocVien,
        [FromQuery(Name = "page")]      int trang = 1,
        [FromQuery(Name = "pageSize")]  int kichThuocTrang = 20,
        CancellationToken maHuy = default)
    {
        return Ok(await dichVuHoaDon.LayDanhSachHoaDonTheoHocVienAsync(
            maHocVien, trang, kichThuocTrang, maHuy));
    }

    /// <summary>
    /// Tính tổng công nợ hiện tại của học sinh.
    /// Công thức: Debt = Σ AmountDue (chưa hủy) − Σ Amount (Confirmed).
    /// </summary>
    [HttpGet("students/{studentId:int}/debt")]
    public async Task<ActionResult<PhanHoiCongNo>> TinhCongNoHocVien(
        [FromRoute(Name = "studentId")] int maHocVien,
        CancellationToken maHuy = default)
    {
        return Ok(await dichVuHoaDon.TinhCongNoHocVienAsync(maHocVien, maHuy));
    }

    /// <summary>
    /// Lấy danh sách học sinh còn công nợ chưa thanh toán (dashboard).
    /// Sắp xếp theo số tiền nợ giảm dần.
    /// </summary>
    [HttpGet("debtors")]
    public async Task<ActionResult<PagedResult<PhanHoiHocVienConNo>>> LayDanhSachHocVienConNo(
        [FromQuery(Name = "page")]     int trang = 1,
        [FromQuery(Name = "pageSize")] int kichThuocTrang = 20,
        CancellationToken maHuy = default)
    {
        return Ok(await dichVuHoaDon.LayDanhSachHocVienConNoAsync(trang, kichThuocTrang, maHuy));
    }
}
