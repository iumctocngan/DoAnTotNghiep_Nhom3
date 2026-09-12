using CmsEdu.Api.ExceptionHandling;
using CmsEdu.Application.Guardians;
using CmsEdu.Domain.Enums;
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
    public async Task<IActionResult> LayDanhSach([FromRoute(Name = "id")] int maHocVien, CancellationToken maHuy) =>
        Ok(await dichVu.LayLienKetAsync(maHocVien, maHuy));

    [HttpPost]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
    public async Task<IActionResult> GanLienKet([FromRoute(Name = "id")] int maHocVien, YeuCauLienKetNguoiGiamHo yeuCau, CancellationToken maHuy)
    {
        await dichVu.GanLienKetAsync(maHocVien, yeuCau, maHuy);
        return NoContent();
    }

    [HttpPut("{guardianId:int}")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
    public async Task<IActionResult> CapNhatLienKet([FromRoute(Name = "id")] int maHocVien,
        [FromRoute(Name = "guardianId")] int maNguoiGiamHo, YeuCauCapNhatLienKetNguoiGiamHo yeuCau, CancellationToken maHuy)
    {
        await dichVu.CapNhatLienKetAsync(maHocVien, maNguoiGiamHo, yeuCau, maHuy);
        return NoContent();
    }

    [HttpPut("{guardianId:int}/primary")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
    public async Task<IActionResult> DatNguoiChinh([FromRoute(Name = "id")] int maHocVien,
        [FromRoute(Name = "guardianId")] int maNguoiGiamHo, CancellationToken maHuy)
    {
        await dichVu.DatNguoiChinhAsync(maHocVien, maNguoiGiamHo, maHuy);
        return NoContent();
    }

    [HttpDelete("{guardianId:int}")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
    public async Task<IActionResult> GoLienKet([FromRoute(Name = "id")] int maHocVien,
        [FromRoute(Name = "guardianId")] int maNguoiGiamHo, CancellationToken maHuy)
    {
        await dichVu.GoLienKetAsync(maHocVien, maNguoiGiamHo, maHuy);
        return NoContent();
    }
}
