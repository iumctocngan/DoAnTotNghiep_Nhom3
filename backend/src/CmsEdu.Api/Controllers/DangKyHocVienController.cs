using System.Data;
using CmsEdu.Api.ExceptionHandling;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Guardians;
using CmsEdu.Application.Students;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Api.Controllers;

public record YeuCauDangKyHocVien(string FullName, DateOnly DateOfBirth, Gender? Gender,
    string? LearningNote, int? GuardianId, YeuCauNguoiGiamHo? NguoiGiamHoMoi, string Relationship, int CourseMonths);

[ApiController]
[Route("api/students/register")]
[Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
[BoLocKiemTraHocVien]
public class DangKyHocVienController(AppDbContext duLieu, IDichVuHocVien hocVien,
    DichVuNguoiGiamHo nguoiGiamHo) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> DangKy(YeuCauDangKyHocVien yeuCau, CancellationToken maHuy)
    {
        if (yeuCau.CourseMonths < 1 || yeuCau.CourseMonths > 120)
            return BadRequest(new { message = "Thời hạn khóa học phải từ 1 đến 120 tháng." });
        if (yeuCau.GuardianId.HasValue == (yeuCau.NguoiGiamHoMoi is not null))
            return BadRequest(new { message = "Chọn người giám hộ có sẵn hoặc nhập một người giám hộ mới." });
        if (string.IsNullOrWhiteSpace(yeuCau.Relationship) || yeuCau.Relationship.Trim().Length > 50)
            return BadRequest(new { message = "Quan hệ với học viên bắt buộc và tối đa 50 ký tự." });

        // Học viên, người giám hộ mới và liên kết cùng thành công hoặc cùng hoàn tác.
        await using var giaoDich = await duLieu.Database.BeginTransactionAsync(IsolationLevel.Serializable, maHuy);
        var maNguoiGiamHo = yeuCau.GuardianId ??
            (await nguoiGiamHo.LuuAsync(null, yeuCau.NguoiGiamHoMoi!, maHuy)).Id;
        var maHocVien = await SinhMaTuDong.TaoAsync(duLieu, "HS", maHuy);
        var ketQua = await hocVien.TaoHocVienAsync(new YeuCauTaoHocVien(maHocVien, yeuCau.FullName,
            yeuCau.DateOfBirth, yeuCau.Gender, yeuCau.LearningNote, yeuCau.CourseMonths), maHuy);
        await nguoiGiamHo.GanLienKetAsync(ketQua.Id,
            new YeuCauLienKetNguoiGiamHo(maNguoiGiamHo, yeuCau.Relationship, true), maHuy);
        await giaoDich.CommitAsync(maHuy);
        return Created($"/api/students/{ketQua.Id}", ketQua);
    }
}
