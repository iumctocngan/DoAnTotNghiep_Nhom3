using CmsEdu.Api.ExceptionHandling;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Students;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/students")]
[Authorize]
[BoLocKiemTraHocVien]
public class HocVienController(IDichVuHocVien dichVuHocVien) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PhanHoiHocVien>>> LayDanhSachHocVien(
        [FromQuery(Name = "search")] string? tuKhoa, [FromQuery(Name = "isArchived")] bool? daLuuTru = false,
        [FromQuery(Name = "page")] int trang = 1, [FromQuery(Name = "pageSize")] int kichThuocTrang = 20, CancellationToken maHuy = default) =>
        Ok(await dichVuHocVien.LayDanhSachHocVienAsync(tuKhoa, daLuuTru, trang, kichThuocTrang, maHuy));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PhanHoiHocVien>> LayHocVien([FromRoute(Name = "id")] int maDinhDanh, CancellationToken maHuy) =>
        Ok(await dichVuHocVien.LayHocVienTheoIdAsync(maDinhDanh, maHuy));

    [HttpPost]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
    public async Task<ActionResult<PhanHoiHocVien>> TaoHocVien(YeuCauTaoHocVien yeuCau, CancellationToken maHuy)
    {
        var hocVien = await dichVuHocVien.TaoHocVienAsync(yeuCau, maHuy);
        return CreatedAtAction(nameof(LayHocVien), new { id = hocVien.Id }, hocVien);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
    public async Task<ActionResult<PhanHoiHocVien>> CapNhatHocVien([FromRoute(Name = "id")] int maDinhDanh, YeuCauCapNhatHocVien yeuCau, CancellationToken maHuy) =>
        Ok(await dichVuHocVien.CapNhatHocVienAsync(maDinhDanh, yeuCau, maHuy));

    [HttpPost("{id:int}/archive")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<IActionResult> LuuTruHocVien([FromRoute(Name = "id")] int maDinhDanh, CancellationToken maHuy)
    {
        await dichVuHocVien.LuuTruHocVienAsync(maDinhDanh, maHuy);
        return NoContent();
    }
}
