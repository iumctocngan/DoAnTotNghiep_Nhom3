using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Application.Classes;
using CmsEdu.Api.Authorization;
using CmsEdu.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/classes")]
[Authorize]
public class LopHocController(DichVuLopHoc dichVu, AppDbContext duLieu) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Classes.View)]
    public async Task<ActionResult<List<ThongTinLopHoc>>> DanhSach(CancellationToken maHuy) =>
        Ok(await dichVu.DanhSach(maHuy));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Classes.View)]
    public async Task<ActionResult<ThongTinLopHoc>> ChiTiet(int id, CancellationToken maHuy) =>
        Ok(await dichVu.ChiTiet(id, maHuy));

    [HttpPost]
    [HasPermission(Permissions.Classes.Manage)]
    public async Task<ActionResult<ThongTinLopHoc>> Tao(YeuCauLopHoc yeuCau, CancellationToken maHuy)
    {
        yeuCau = yeuCau with { MaLop = await SinhMaTuDong.TaoAsync(duLieu, "LH", maHuy) };
        var lop = await dichVu.Tao(yeuCau, maHuy);
        return CreatedAtAction(nameof(ChiTiet), new { id = lop.Id }, lop);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.Classes.Manage)]
    public async Task<ActionResult<ThongTinLopHoc>> CapNhat(int id, YeuCauLopHoc yeuCau,
        CancellationToken maHuy) => Ok(await dichVu.CapNhat(id, yeuCau, maHuy));

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.Classes.Delete)]
    public async Task<IActionResult> Xoa(int id, CancellationToken maHuy)
    {
        await dichVu.Xoa(id, maHuy);
        return NoContent();
    }
}
