using System.Data;
using Microsoft.EntityFrameworkCore;
using CmsEdu.Domain.Entities;
using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Api.ExceptionHandling;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Students;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CmsEdu.Api.Controllers;

public record YeuCauGiaHan(int CourseMonths);

[ApiController]
[Route("api/students")]
[Authorize]
[BoLocKiemTraHocVien]
public class HocVienController(IDichVuHocVien dichVuHocVien, AppDbContext duLieu) : ControllerBase
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
        yeuCau = yeuCau with { StudentCode = await SinhMaTuDong.TaoAsync(duLieu, "HS", maHuy) };
        var hocVien = await dichVuHocVien.TaoHocVienAsync(yeuCau, maHuy);
        return CreatedAtAction(nameof(LayHocVien), new { id = hocVien.Id }, hocVien);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
    public async Task<ActionResult<PhanHoiHocVien>> CapNhatHocVien([FromRoute(Name = "id")] int maDinhDanh, YeuCauCapNhatHocVien yeuCau, CancellationToken maHuy) =>
        Ok(await dichVuHocVien.CapNhatHocVienAsync(maDinhDanh, yeuCau, maHuy));

    [HttpPost("{id:int}/renew")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
    public async Task<IActionResult> GiaHan(int id, YeuCauGiaHan yeuCau, CancellationToken maHuy)
    {
        if (yeuCau.CourseMonths < 1 || yeuCau.CourseMonths > 120)
            return BadRequest(new { message = "Thời hạn gia hạn phải từ 1 đến 120 tháng." });
        await using var tx = await duLieu.Database.BeginTransactionAsync(IsolationLevel.Serializable, maHuy);
        var hocVien = await duLieu.Students.SingleOrDefaultAsync(x => x.Id == id, maHuy);
        if (hocVien is null) return NotFound(new { message = "Không tìm thấy học viên." });
        if (hocVien.IsArchived) return Conflict(new { message = "Cần khôi phục hồ sơ trước khi gia hạn." });
        hocVien.CourseMonths = checked(hocVien.CourseMonths + yeuCau.CourseMonths);
        hocVien.RemainingSessions = checked(hocVien.RemainingSessions + yeuCau.CourseMonths * 4);
        duLieu.AuditLogs.Add(new AuditLog {
            UserId = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            Action = "Student.Renew", EntityType = nameof(Student), EntityId = id.ToString(),
            Description = $"Gia hạn {yeuCau.CourseMonths} tháng, cộng {yeuCau.CourseMonths * 4} buổi.",
            OccurredAt = DateTime.UtcNow
        });
        await duLieu.SaveChangesAsync(maHuy);
        await tx.CommitAsync(maHuy);
        return Ok(await dichVuHocVien.LayHocVienTheoIdAsync(id, maHuy));
    }

    [HttpPost("{id:int}/archive")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<IActionResult> LuuTruHocVien([FromRoute(Name = "id")] int maDinhDanh, CancellationToken maHuy)
    {
        await dichVuHocVien.LuuTruHocVienAsync(maDinhDanh, maHuy);
        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<IActionResult> KhoiPhucHocVien([FromRoute(Name = "id")] int maDinhDanh, CancellationToken maHuy)
    {
        await dichVuHocVien.KhoiPhucHocVienAsync(maDinhDanh, maHuy);
        return NoContent();
    }
}
