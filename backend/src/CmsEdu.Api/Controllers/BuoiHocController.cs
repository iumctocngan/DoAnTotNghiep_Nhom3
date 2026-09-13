using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Sessions;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

/// <summary>
/// Cung cấp API quản lý buổi học: xem, tạo, cập nhật, hủy và hoàn tất buổi học.
/// </summary>
[ApiController]
[Route("api/sessions")]
[Authorize]
public class BuoiHocController(IDichVuBuoiHoc dichVuBuoiHoc) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách buổi học theo phạm vi quyền, có thể lọc theo lớp và khoảng ngày.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PhanHoiBuoiHoc>>> LayDanhSachBuoiHoc(
        [FromQuery(Name = "classId")] int? maLop,
        [FromQuery(Name = "fromDate")] DateOnly? tuNgay,
        [FromQuery(Name = "toDate")] DateOnly? denNgay,
        [FromQuery(Name = "page")] int trang = 1,
        [FromQuery(Name = "pageSize")] int kichThuocTrang = 20,
        CancellationToken maHuy = default)
    {
        return Ok(await dichVuBuoiHoc.LayDanhSachBuoiHocAsync(
            maLop, tuNgay, denNgay, trang, kichThuocTrang, maHuy));
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một buổi học theo phạm vi quyền của người dùng hiện tại.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PhanHoiBuoiHoc>> LayChiTietBuoiHoc(
        [FromRoute(Name = "id")] int maBuoiHoc,
        CancellationToken maHuy)
    {
        return Ok(await dichVuBuoiHoc.LayChiTietBuoiHocTheoIdAsync(maBuoiHoc, maHuy));
    }

    /// <summary>
    /// Tạo một buổi học mới cho lớp đang hoạt động. Chỉ Admin được thực hiện.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<PhanHoiBuoiHoc>> TaoBuoiHoc(
        [FromBody] YeuCauTaoBuoiHoc yeuCau,
        CancellationToken maHuy)
    {
        var buoiHoc = await dichVuBuoiHoc.TaoBuoiHocAsync(yeuCau, maHuy);
        return CreatedAtAction(nameof(LayChiTietBuoiHoc), new { id = buoiHoc.Id }, buoiHoc);
    }

    /// <summary>
    /// Cập nhật buổi học đang ở trạng thái lên lịch (Scheduled). Chỉ Admin được thực hiện.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<PhanHoiBuoiHoc>> CapNhatBuoiHoc(
        [FromRoute(Name = "id")] int maBuoiHoc,
        [FromBody] YeuCauCapNhatBuoiHoc yeuCau,
        CancellationToken maHuy)
    {
        return Ok(await dichVuBuoiHoc.CapNhatBuoiHocAsync(maBuoiHoc, yeuCau, maHuy));
    }

    /// <summary>
    /// Hủy buổi học đang ở trạng thái lên lịch (Scheduled). Chỉ Admin được thực hiện.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<PhanHoiBuoiHoc>> HuyBuoiHoc(
        [FromRoute(Name = "id")] int maBuoiHoc,
        CancellationToken maHuy)
    {
        return Ok(await dichVuBuoiHoc.HuyBuoiHocAsync(maBuoiHoc, maHuy));
    }

    /// <summary>
    /// Hoàn tất buổi học đang ở trạng thái lên lịch (Scheduled) khi đã điểm danh đủ học viên hợp lệ.
    /// </summary>
    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.Teacher}")]
    public async Task<ActionResult<PhanHoiBuoiHoc>> HoanTatBuoiHoc(
        [FromRoute(Name = "id")] int maBuoiHoc,
        CancellationToken maHuy)
    {
        return Ok(await dichVuBuoiHoc.HoanTatBuoiHocAsync(maBuoiHoc, maHuy));
    }
}
