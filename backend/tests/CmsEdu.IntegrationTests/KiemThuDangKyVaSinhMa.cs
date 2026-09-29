using System.Net;
using System.Net.Http.Json;
using CmsEdu.Api.Controllers;
using CmsEdu.Application.Guardians;
using CmsEdu.Application.Students;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UngDungKiemThu = CmsEdu.IntegrationTests.KiemThuApiHocVien.UngDungKiemThu;

namespace CmsEdu.IntegrationTests;

public class KiemThuDangKyVaSinhMa
{
    [KiemThuSqlServer]
    public async Task DangKyDungChungNguoiGiamHoVaHoanTacKhiLoi()
    {
        await using var ungDung = new UngDungKiemThu();
        await ungDung.KhoiTaoAsync();
        using var khach = ungDung.TaoTrinhKhach();
        var yeuCau = new YeuCauDangKyHocVien("Học viên một", new(2018, 1, 1), null, null, null,
            new YeuCauNguoiGiamHo("Người giám hộ", "0912345678", null), "Mẹ");
        var phanHoi = await khach.PostAsJsonAsync("/api/students/register", yeuCau);
        Assert.Equal(HttpStatusCode.Created, phanHoi.StatusCode);
        var dau = (await phanHoi.Content.ReadFromJsonAsync<PhanHoiHocVien>())!;
        Assert.Equal("HS0001", dau.StudentCode);
        var lienKet = (await khach.GetFromJsonAsync<List<PhanHoiLienKetNguoiGiamHo>>($"/api/students/{dau.Id}/guardians"))!;
        var nguoi = Assert.Single(lienKet);
        var phanHoiHai = await khach.PostAsJsonAsync("/api/students/register", yeuCau with {
            FullName = "Học viên hai", GuardianId = nguoi.GuardianId, NguoiGiamHoMoi = null });
        Assert.Equal(HttpStatusCode.Created, phanHoiHai.StatusCode);
        Assert.Equal("HS0002", (await phanHoiHai.Content.ReadFromJsonAsync<PhanHoiHocVien>())!.StudentCode);
        var loi = await khach.PostAsJsonAsync("/api/students/register", yeuCau with { DateOfBirth = new(2099, 1, 1) });
        Assert.Equal(HttpStatusCode.BadRequest, loi.StatusCode);
        using var phamVi = ungDung.Services.CreateScope();
        var duLieu = phamVi.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await duLieu.Students.CountAsync());
        Assert.Equal(1, await duLieu.Guardians.CountAsync());
        Assert.Equal(2, await duLieu.StudentGuardians.CountAsync());
        using var keToan = ungDung.TaoTrinhKhach("Accountant");
        Assert.Equal(HttpStatusCode.Forbidden, (await keToan.PostAsJsonAsync("/api/students/register", yeuCau)).StatusCode);
    }

    [KiemThuSqlServer]
    public async Task SinhMaDongThoiKhongTrungVaKhongTaiSuDung()
    {
        await using var ungDung = new UngDungKiemThu();
        await ungDung.KhoiTaoAsync();
        var danhSach = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ => {
            using var phamVi = ungDung.Services.CreateScope();
            return await SinhMaTuDong.TaoAsync(phamVi.ServiceProvider.GetRequiredService<AppDbContext>(), "HS", default);
        }));
        Assert.Equal(12, danhSach.Distinct().Count());
        Assert.All(danhSach, ma => Assert.Matches("^HS[0-9]{4}$", ma));
        using var phamViSau = ungDung.Services.CreateScope();
        var duLieu = phamViSau.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("GV001", await SinhMaTuDong.TaoAsync(duLieu, "GV", default));
        Assert.Equal("CSKH001", await SinhMaTuDong.TaoAsync(duLieu, "CSKH", default));
        Assert.Equal("HS0013", await SinhMaTuDong.TaoAsync(duLieu, "HS", default));
    }
}
