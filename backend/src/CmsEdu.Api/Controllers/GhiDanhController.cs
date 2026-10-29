using CmsEdu.Application.Enrollments;
using CmsEdu.Api.Authorization;
using CmsEdu.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/enrollments")]
[Authorize]
public class GhiDanhController(DichVuGhiDanh dichVu) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Enrollments.View)]
    public async Task<ActionResult<List<ThongTinGhiDanh>>> DanhSach(
        [FromQuery] int? lopId, [FromQuery] int? hocVienId, CancellationToken maHuy) =>
        Ok(await dichVu.DanhSach(lopId, hocVienId, maHuy));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.Enrollments.View)]
    public async Task<ActionResult<ThongTinGhiDanh>> ChiTiet(int id, CancellationToken maHuy) =>
        Ok(await dichVu.ChiTiet(id, maHuy));

    [HttpPost]
    [HasPermission(Permissions.Enrollments.Manage)]
    public async Task<ActionResult<ThongTinGhiDanh>> Tao(YeuCauGhiDanh yeuCau, CancellationToken maHuy)
    {
        var ghiDanh = await dichVu.Tao(yeuCau, maHuy);
        return CreatedAtAction(nameof(ChiTiet), new { id = ghiDanh.Id }, ghiDanh);
    }

    [HttpPost("{id:int}/pause")]
    [HasPermission(Permissions.Enrollments.Manage)]
    public async Task<ActionResult<ThongTinGhiDanh>> BaoLuu(int id, YeuCauBaoLuu yeuCau,
        CancellationToken maHuy) => Ok(await dichVu.BaoLuu(id, yeuCau, maHuy));

    [HttpPost("{id:int}/resume")]
    [HasPermission(Permissions.Enrollments.Manage)]
    public async Task<ActionResult<ThongTinGhiDanh>> TroLai(int id, CancellationToken maHuy) =>
        Ok(await dichVu.TroLai(id, maHuy));

    [HttpPost("{id:int}/complete")]
    [HasPermission(Permissions.Enrollments.Manage)]
    public async Task<ActionResult<ThongTinGhiDanh>> HoanThanh(int id,
        KetThucYeuCauGhiDanh yeuCau, CancellationToken maHuy) =>
        Ok(await dichVu.HoanThanh(id, yeuCau, maHuy));

    [HttpPost("{id:int}/withdraw")]
    [HasPermission(Permissions.Enrollments.Manage)]
    public async Task<ActionResult<ThongTinGhiDanh>> NghiHoc(int id,
        KetThucYeuCauGhiDanh yeuCau, CancellationToken maHuy) =>
        Ok(await dichVu.NghiHoc(id, yeuCau, maHuy));
}
