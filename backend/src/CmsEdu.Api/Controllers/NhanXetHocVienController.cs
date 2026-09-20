using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Remarks;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{UserRole.Admin},{UserRole.Teacher},{UserRole.CustomerCare}")]
public class NhanXetHocVienController(DichVuNhanXetHocVien dichVu) : ControllerBase
{
    [HttpGet("api/enrollments/{id:int}/remarks")]
    public async Task<ActionResult<PagedResult<ThongTinNhanXet>>> LayDanhSach(
        [FromRoute(Name = "id")] int maGhiDanh,
        [FromQuery(Name = "page")] int trang = 1,
        [FromQuery(Name = "pageSize")] int kichThuocTrang = 20,
        CancellationToken maHuy = default) =>
        Ok(await dichVu.LayDanhSach(maGhiDanh, trang, kichThuocTrang, maHuy));

    [HttpGet("api/remarks/{id:int}")]
    public async Task<ActionResult<ThongTinNhanXet>> LayChiTiet(
        [FromRoute(Name = "id")] int maNhanXet, CancellationToken maHuy) =>
        Ok(await dichVu.LayChiTiet(maNhanXet, maHuy));

    [HttpPost("api/enrollments/{id:int}/remarks")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.Teacher}")]
    public async Task<ActionResult<ThongTinNhanXet>> Tao(
        [FromRoute(Name = "id")] int maGhiDanh, YeuCauTaoNhanXet yeuCau, CancellationToken maHuy)
    {
        var nhanXet = await dichVu.Tao(maGhiDanh, yeuCau, maHuy);
        return CreatedAtAction(nameof(LayChiTiet), new { id = nhanXet.Id }, nhanXet);
    }

    [HttpPut("api/remarks/{id:int}")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.Teacher}")]
    public async Task<ActionResult<ThongTinNhanXet>> Sua(
        [FromRoute(Name = "id")] int maNhanXet, YeuCauSuaNhanXet yeuCau, CancellationToken maHuy) =>
        Ok(await dichVu.Sua(maNhanXet, yeuCau, maHuy));
}
