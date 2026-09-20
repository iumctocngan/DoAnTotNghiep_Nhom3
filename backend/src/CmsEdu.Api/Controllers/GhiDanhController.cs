using CmsEdu.Application.Enrollments;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/enrollments")]
[Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
public class GhiDanhController(DichVuGhiDanh dichVu) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ThongTinGhiDanh>>> DanhSach(
        [FromQuery] int? lopId, [FromQuery] int? hocVienId, CancellationToken maHuy) =>
        Ok(await dichVu.DanhSach(lopId, hocVienId, maHuy));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ThongTinGhiDanh>> ChiTiet(int id, CancellationToken maHuy) =>
        Ok(await dichVu.ChiTiet(id, maHuy));

    [HttpPost]
    public async Task<ActionResult<ThongTinGhiDanh>> Tao(YeuCauGhiDanh yeuCau, CancellationToken maHuy)
    {
        var ghiDanh = await dichVu.Tao(yeuCau, maHuy);
        return CreatedAtAction(nameof(ChiTiet), new { id = ghiDanh.Id }, ghiDanh);
    }

    [HttpPost("{id:int}/pause")]
    public async Task<ActionResult<ThongTinGhiDanh>> BaoLuu(int id, YeuCauBaoLuu yeuCau,
        CancellationToken maHuy) => Ok(await dichVu.BaoLuu(id, yeuCau, maHuy));

    [HttpPost("{id:int}/resume")]
    public async Task<ActionResult<ThongTinGhiDanh>> TroLai(int id, CancellationToken maHuy) =>
        Ok(await dichVu.TroLai(id, maHuy));

    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<ThongTinGhiDanh>> HoanThanh(int id,
        KetThucYeuCauGhiDanh yeuCau, CancellationToken maHuy) =>
        Ok(await dichVu.HoanThanh(id, yeuCau, maHuy));

    [HttpPost("{id:int}/withdraw")]
    public async Task<ActionResult<ThongTinGhiDanh>> NghiHoc(int id,
        KetThucYeuCauGhiDanh yeuCau, CancellationToken maHuy) =>
        Ok(await dichVu.NghiHoc(id, yeuCau, maHuy));
}
