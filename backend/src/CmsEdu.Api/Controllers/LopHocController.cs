using CmsEdu.Application.Classes;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/classes")]
[Authorize(Roles = UserRole.Admin)]
public class LopHocController(DichVuLopHoc dichVu) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ThongTinLopHoc>>> DanhSach(CancellationToken maHuy) =>
        Ok(await dichVu.DanhSach(maHuy));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ThongTinLopHoc>> ChiTiet(int id, CancellationToken maHuy) =>
        Ok(await dichVu.ChiTiet(id, maHuy));

    [HttpPost]
    public async Task<ActionResult<ThongTinLopHoc>> Tao(YeuCauLopHoc yeuCau, CancellationToken maHuy)
    {
        var lop = await dichVu.Tao(yeuCau, maHuy);
        return CreatedAtAction(nameof(ChiTiet), new { id = lop.Id }, lop);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ThongTinLopHoc>> CapNhat(int id, YeuCauLopHoc yeuCau,
        CancellationToken maHuy) => Ok(await dichVu.CapNhat(id, yeuCau, maHuy));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Xoa(int id, CancellationToken maHuy)
    {
        await dichVu.Xoa(id, maHuy);
        return NoContent();
    }
}
