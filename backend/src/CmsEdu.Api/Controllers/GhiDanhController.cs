using CmsEdu.Application.Enrollments;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/enrollments")]
[Authorize]
public class GhiDanhController(DichVuGhiDanh dichVu) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.Accountant},{UserRole.CustomerCare}")]
    public async Task<ActionResult<List<ThongTinGhiDanh>>> DanhSach(
        [FromQuery] int? lopId, [FromQuery] int? hocVienId, CancellationToken maHuy) =>
        Ok(await dichVu.DanhSach(lopId, hocVienId, maHuy));

    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.Teacher},{UserRole.Accountant},{UserRole.CustomerCare}")]
    public async Task<ActionResult<ThongTinGhiDanh>> ChiTiet(int id, CancellationToken maHuy) =>
        Ok(await dichVu.ChiTiet(id, maHuy));

    [HttpPost]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
    public async Task<ActionResult<ThongTinGhiDanh>> Tao(YeuCauGhiDanh yeuCau, CancellationToken maHuy)
    {
        var ghiDanh = await dichVu.Tao(yeuCau, maHuy);
        return CreatedAtAction(nameof(ChiTiet), new { id = ghiDanh.Id }, ghiDanh);
    }

    [HttpPost("{id:int}/pause")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
    public async Task<ActionResult<ThongTinGhiDanh>> BaoLuu(int id, YeuCauBaoLuu yeuCau,
        CancellationToken maHuy) => Ok(await dichVu.BaoLuu(id, yeuCau, maHuy));

    [HttpPost("{id:int}/resume")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
    public async Task<ActionResult<ThongTinGhiDanh>> TroLai(int id, CancellationToken maHuy) =>
        Ok(await dichVu.TroLai(id, maHuy));

    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<ThongTinGhiDanh>> HoanThanh(int id,
        KetThucYeuCauGhiDanh yeuCau, CancellationToken maHuy) =>
        Ok(await dichVu.HoanThanh(id, yeuCau, maHuy));

    [HttpPost("{id:int}/withdraw")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
    public async Task<ActionResult<ThongTinGhiDanh>> NghiHoc(int id,
        KetThucYeuCauGhiDanh yeuCau, CancellationToken maHuy) =>
        Ok(await dichVu.NghiHoc(id, yeuCau, maHuy));
}
