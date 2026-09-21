using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CmsEdu.Application.Authentication;
using CmsEdu.Application.Classes;
using CmsEdu.Application.Enrollments;
using CmsEdu.Application.Remarks;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Identity;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CmsEdu.IntegrationTests;

public class KiemThuApiLopGhiDanhNhanXet
{
    [KiemThuSqlServer]
    public async Task TaoLopGhiDanhBaoLuuVaNhanXet_KiemTraQuyenVaRangBuoc()
    {
        await using var ungDung = new AuthenticationApiTests.AuthenticationTestApp();
        await ungDung.InitializeAsync();
        var (capDoId, hocVien1, hocVien2, giaoVienId) = await TaoDuLieuNen(ungDung);
        using var admin = await DangNhap(ungDung, AuthenticationApiTests.AuthenticationTestApp.Email,
            AuthenticationApiTests.AuthenticationTestApp.Password);
        using var giaoVien = await DangNhap(ungDung, "giaovien@test.local", "Password123");
        using var chamSoc = await DangNhap(ungDung, "chamsoc@test.local", "Password123");
        using var chuaDangNhap = ungDung.CreateTestClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await chuaDangNhap.GetAsync("/api/classes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await chamSoc.GetAsync("/api/classes")).StatusCode);

        var homNay = DateOnly.FromDateTime(DateTime.Today);
        var yeuCauLop = new YeuCauLopHoc("LOP-KT-01", "Lớp kiểm thử", capDoId, giaoVienId,
            1, homNay.AddDays(-1), homNay.AddDays(30), DayOfWeek.Monday,
            new TimeOnly(8, 0), new TimeOnly(10, 0), ClassStatus.Active);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/classes",
            yeuCauLop with { SiSoToiDa = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await giaoVien.PostAsJsonAsync("/api/classes", yeuCauLop)).StatusCode);
        var taoLop = await admin.PostAsJsonAsync("/api/classes", yeuCauLop);
        Assert.Equal(HttpStatusCode.Created, taoLop.StatusCode);
        var lop = (await taoLop.Content.ReadFromJsonAsync<ThongTinLopHoc>())!;
        Assert.Equal(0, lop.SiSoHienTai);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/classes", yeuCauLop)).StatusCode);
        Assert.Equal(lop.Id, (await admin.GetFromJsonAsync<ThongTinLopHoc>($"/api/classes/{lop.Id}"))!.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/classes/999999")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await giaoVien.PostAsJsonAsync("/api/enrollments",
            new YeuCauGhiDanh(hocVien1, lop.Id, homNay))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await chamSoc.PostAsJsonAsync("/api/enrollments",
            new YeuCauGhiDanh(999999, lop.Id, homNay))).StatusCode);
        var taoGhiDanh = await chamSoc.PostAsJsonAsync("/api/enrollments",
            new YeuCauGhiDanh(hocVien1, lop.Id, homNay));
        Assert.Equal(HttpStatusCode.Created, taoGhiDanh.StatusCode);
        var ghiDanh = (await taoGhiDanh.Content.ReadFromJsonAsync<ThongTinGhiDanh>())!;
        Assert.Equal(1, (await admin.GetFromJsonAsync<ThongTinLopHoc>($"/api/classes/{lop.Id}"))!.SiSoHienTai);
        Assert.Equal(HttpStatusCode.Conflict, (await chamSoc.PostAsJsonAsync("/api/enrollments",
            new YeuCauGhiDanh(hocVien2, lop.Id, homNay))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/classes/{lop.Id}",
            yeuCauLop with { TrangThai = ClassStatus.Completed })).StatusCode);

        var baoLuu = await chamSoc.PostAsJsonAsync($"/api/enrollments/{ghiDanh.Id}/pause",
            new YeuCauBaoLuu("Tạm nghỉ", homNay.AddDays(7)));
        Assert.Equal(HttpStatusCode.OK, baoLuu.StatusCode);
        Assert.Equal(EnrollmentStatus.Paused,
            (await chamSoc.GetFromJsonAsync<ThongTinGhiDanh>($"/api/enrollments/{ghiDanh.Id}"))!.TrangThai);
        Assert.Equal(1, (await admin.GetFromJsonAsync<ThongTinLopHoc>($"/api/classes/{lop.Id}"))!.SiSoHienTai);
        var troLai = await chamSoc.PostAsync($"/api/enrollments/{ghiDanh.Id}/resume", null);
        Assert.Equal(HttpStatusCode.OK, troLai.StatusCode);
        Assert.Equal(EnrollmentStatus.Active,
            (await admin.GetFromJsonAsync<ThongTinGhiDanh>($"/api/enrollments/{ghiDanh.Id}"))!.TrangThai);

        Assert.Equal(HttpStatusCode.Forbidden, (await chamSoc.PostAsJsonAsync(
            $"/api/enrollments/{ghiDanh.Id}/remarks", new YeuCauTaoNhanXet(null, "Tốt"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await giaoVien.PostAsJsonAsync(
            $"/api/enrollments/{ghiDanh.Id}/remarks", new YeuCauTaoNhanXet(null, " "))).StatusCode);
        var taoNhanXet = await giaoVien.PostAsJsonAsync($"/api/enrollments/{ghiDanh.Id}/remarks",
            new YeuCauTaoNhanXet(null, "Tiến bộ tốt"));
        Assert.Equal(HttpStatusCode.Created, taoNhanXet.StatusCode);
        var nhanXet = (await taoNhanXet.Content.ReadFromJsonAsync<ThongTinNhanXet>())!;
        Assert.Equal(giaoVienId, nhanXet.NguoiTao);
        Assert.Equal("Tiến bộ tốt", (await admin.GetFromJsonAsync<ThongTinNhanXet>(
            $"/api/remarks/{nhanXet.Id}"))!.NoiDung);
        Assert.Equal(HttpStatusCode.Forbidden, (await chamSoc.PutAsJsonAsync($"/api/remarks/{nhanXet.Id}",
            new YeuCauSuaNhanXet("Sửa"))).StatusCode);
        var sua = await giaoVien.PutAsJsonAsync($"/api/remarks/{nhanXet.Id}",
            new YeuCauSuaNhanXet("Đã tiến bộ"));
        Assert.Equal(HttpStatusCode.OK, sua.StatusCode);
        Assert.Equal("Đã tiến bộ", (await chamSoc.GetFromJsonAsync<ThongTinNhanXet>(
            $"/api/remarks/{nhanXet.Id}"))!.NoiDung);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/remarks/999999")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await chamSoc.PostAsJsonAsync(
            $"/api/enrollments/{ghiDanh.Id}/complete",
            new KetThucYeuCauGhiDanh("Đã xong", homNay))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(
            $"/api/enrollments/{ghiDanh.Id}/complete",
            new KetThucYeuCauGhiDanh("Đã xong", homNay))).StatusCode);
        Assert.Equal(0, (await admin.GetFromJsonAsync<ThongTinLopHoc>($"/api/classes/{lop.Id}"))!.SiSoHienTai);
        Assert.Equal(HttpStatusCode.Conflict, (await giaoVien.PostAsJsonAsync(
            $"/api/enrollments/{ghiDanh.Id}/remarks", new YeuCauTaoNhanXet(null, "Sau khi hoàn thành"))).StatusCode);

        using var phamVi = ungDung.Services.CreateScope();
        var duLieu = phamVi.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Đã tiến bộ", (await duLieu.StudentRemarks.AsNoTracking()
            .SingleAsync(x => x.Id == nhanXet.Id)).Content);
    }

    private static async Task<(int, int, int, string)> TaoDuLieuNen(
        AuthenticationApiTests.AuthenticationTestApp ungDung)
    {
        using var phamVi = ungDung.Services.CreateScope();
        var quanLyNguoiDung = phamVi.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var giaoVien = new ApplicationUser { UserName = "giaovien@test.local", Email = "giaovien@test.local",
            EmailConfirmed = true, EmployeeCode = "GV-KT-01", FullName = "Giáo viên kiểm thử" };
        var chamSoc = new ApplicationUser { UserName = "chamsoc@test.local", Email = "chamsoc@test.local",
            EmailConfirmed = true, EmployeeCode = "CS-KT-01", FullName = "Chăm sóc kiểm thử" };
        Assert.True((await quanLyNguoiDung.CreateAsync(giaoVien, "Password123")).Succeeded);
        Assert.True((await quanLyNguoiDung.CreateAsync(chamSoc, "Password123")).Succeeded);
        Assert.True((await quanLyNguoiDung.AddToRoleAsync(giaoVien, UserRole.Teacher)).Succeeded);
        Assert.True((await quanLyNguoiDung.AddToRoleAsync(chamSoc, UserRole.CustomerCare)).Succeeded);
        var duLieu = phamVi.ServiceProvider.GetRequiredService<AppDbContext>();
        var capDo = new Level { Code = "CAP-KT-01", Name = "Cấp kiểm thử", SortOrder = 1,
            Course = new Course { Code = "KHOA-KT-01", Name = "Khóa kiểm thử" } };
        var hocVien1 = new Student { StudentCode = "HV-KT-01", FullName = "Học viên 1",
            DateOfBirth = new DateOnly(2014, 1, 1) };
        var hocVien2 = new Student { StudentCode = "HV-KT-02", FullName = "Học viên 2",
            DateOfBirth = new DateOnly(2014, 1, 1) };
        duLieu.Levels.Add(capDo);
        duLieu.Students.AddRange(hocVien1, hocVien2);
        await duLieu.SaveChangesAsync();
        return (capDo.Id, hocVien1.Id, hocVien2.Id, giaoVien.Id);
    }

    private static async Task<HttpClient> DangNhap(AuthenticationApiTests.AuthenticationTestApp ungDung,
        string email, string matKhau)
    {
        var trinhKhach = ungDung.CreateTestClient();
        var phanHoi = await trinhKhach.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, matKhau));
        Assert.Equal(HttpStatusCode.OK, phanHoi.StatusCode);
        var duLieu = (await phanHoi.Content.ReadFromJsonAsync<LoginResponse>())!;
        trinhKhach.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", duLieu.AccessToken);
        return trinhKhach;
    }
}
