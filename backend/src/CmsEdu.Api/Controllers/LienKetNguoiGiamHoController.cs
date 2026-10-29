using CmsEdu.Api.Authorization;
using CmsEdu.Api.ExceptionHandling;
using CmsEdu.Application.Guardians;
using CmsEdu.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/students/{id:int}/guardians")]
[Authorize]
[BoLocKiemTraHocVien]
public sealed class LienKetNguoiGiamHoController(DichVuNguoiGiamHo dichVu) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Guardians.View)]
    public async Task<IActionResult> LayDanhSach([FromRoute(Name = "id")] int maHocVien, CancellationToken maHuy) =>
        Ok(await dichVu.LayLienKetAsync(maHocVien, maHuy));

    [HttpPost]
    [HasPermission(Permissions.Guardians.Manage)]
    public async Task<IActionResult> GanLienKet([FromRoute(Name = "id")] int maHocVien, YeuCauLienKetNguoiGiamHo yeuCau, CancellationToken maHuy)
    {
        await dichVu.GanLienKetAsync(maHocVien, yeuCau, maHuy);
        return NoContent();
    }

    [HttpPut("{guardianId:int}")]
    [HasPermission(Permissions.Guardians.Manage)]
    public async Task<IActionResult> CapNhatLienKet([FromRoute(Name = "id")] int maHocVien,
        [FromRoute(Name = "guardianId")] int maNguoiGiamHo, YeuCauCapNhatLienKetNguoiGiamHo yeuCau, CancellationToken maHuy)
    {
        await dichVu.CapNhatLienKetAsync(maHocVien, maNguoiGiamHo, yeuCau, maHuy);
        return NoContent();
    }

    [HttpPut("{guardianId:int}/primary")]
    [HasPermission(Permissions.Guardians.Manage)]
    public async Task<IActionResult> DatNguoiChinh([FromRoute(Name = "id")] int maHocVien,
        [FromRoute(Name = "guardianId")] int maNguoiGiamHo, CancellationToken maHuy)
    {
        await dichVu.DatNguoiChinhAsync(maHocVien, maNguoiGiamHo, maHuy);
        return NoContent();
    }

    [HttpDelete("{guardianId:int}")]
    [HasPermission(Permissions.Guardians.Manage)]
    public async Task<IActionResult> GoLienKet([FromRoute(Name = "id")] int maHocVien,
        [FromRoute(Name = "guardianId")] int maNguoiGiamHo, CancellationToken maHuy)
    {
        await dichVu.GoLienKetAsync(maHocVien, maNguoiGiamHo, maHuy);
        return NoContent();
    }
}
