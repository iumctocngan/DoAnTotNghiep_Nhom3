using CmsEdu.Api.Authorization;
using CmsEdu.Api.ExceptionHandling;
using CmsEdu.Application.Guardians;
using CmsEdu.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/guardians")]
[Authorize]
[BoLocKiemTraHocVien]
public sealed class NguoiGiamHoController(DichVuNguoiGiamHo dichVu) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Guardians.View)]
    public async Task<IActionResult> LayDanhSach([FromQuery(Name = "search")] string? tuKhoa,
        [FromQuery(Name = "isActive")] bool? dangHoatDong = null, [FromQuery(Name = "page")] int trang = 1,
        [FromQuery(Name = "pageSize")] int kichThuocTrang = 20, CancellationToken maHuy = default) =>
        Ok(await dichVu.LayDanhSachAsync(tuKhoa, dangHoatDong, trang, kichThuocTrang, maHuy));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Guardians.View)]
    public async Task<IActionResult> LayTheoId([FromRoute(Name = "id")] int maNguoiGiamHo, CancellationToken maHuy) =>
        Ok(await dichVu.LayTheoIdAsync(maNguoiGiamHo, maHuy));

    [HttpPost]
    [HasPermission(Permissions.Guardians.Manage)]
    public async Task<IActionResult> Tao(YeuCauNguoiGiamHo yeuCau, CancellationToken maHuy)
    {
        var ketQua = await dichVu.LuuAsync(null, yeuCau, maHuy);
        return CreatedAtAction(nameof(LayTheoId), new { id = ketQua.Id }, ketQua);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Guardians.Manage)]
    public async Task<IActionResult> CapNhat([FromRoute(Name = "id")] int maNguoiGiamHo, YeuCauNguoiGiamHo yeuCau, CancellationToken maHuy) =>
        Ok(await dichVu.LuuAsync(maNguoiGiamHo, yeuCau, maHuy));

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Guardians.Delete)]
    public async Task<IActionResult> Xoa([FromRoute(Name = "id")] int maNguoiGiamHo, CancellationToken maHuy)
    {
        await dichVu.XoaAsync(maNguoiGiamHo, maHuy);
        return NoContent();
    }
}
