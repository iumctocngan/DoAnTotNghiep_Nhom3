using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CmsEdu.Application.Authentication;
using CmsEdu.Application.Classes;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Enrollments;
using CmsEdu.Application.Remarks;
using CmsEdu.Application.Sessions;
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
    public async Task DongBoLichTuanLuuBuoiThucVaCapNhatGioTheoLop()
    {
        await using var ungDung = new AuthenticationApiTests.AuthenticationTestApp();
        await ungDung.InitializeAsync();
        var (capDoId, _, _, giaoVienId) = await TaoDuLieuNen(ungDung);
        using var admin = await DangNhap(ungDung, AuthenticationApiTests.AuthenticationTestApp.Email,
            AuthenticationApiTests.AuthenticationTestApp.Password);
        using var scope = ungDung.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var upcoming = today.AddDays(3);
        var lop = new Class
        {
            ClassCode = "CU01", Name = "Lớp lịch tuần", MainTeacherUserId = giaoVienId,
            LevelId = capDoId, Capacity = 10, StartDate = today.AddDays(-30), DayOfWeek = upcoming.DayOfWeek,
            StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 50), Status = ClassStatus.Active
        };
        db.Classes.Add(lop);
        await db.SaveChangesAsync();
        db.Sessions.Add(new Session { ClassId = lop.Id, SessionDate = upcoming.AddDays(1),
            StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(9, 0), IsRegular = false, Note = "Học bù" });
        await db.SaveChangesAsync();

        var url = $"/api/sessions/weekly-sync?classId={lop.Id}&fromDate={today:yyyy-MM-dd}&toDate={today.AddDays(6):yyyy-MM-dd}";
        using var sync = await admin.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.NoContent, sync.StatusCode);
        var list = await admin.GetFromJsonAsync<PagedResult<PhanHoiBuoiHoc>>($"/api/sessions?classId={lop.Id}");
        var saved = Assert.Single(list!.Items, x => x.FollowsClassSchedule);
        Assert.Equal(upcoming, saved.SessionDate);
        Assert.Equal(SessionStatus.Scheduled, saved.Status);
        Assert.Equal(lop.StartTime, saved.StartTime);
        Assert.Equal(lop.EndTime, saved.EndTime);

        using var edit = await admin.PutAsJsonAsync($"/api/classes/{lop.Id}",
            new YeuCauLopHoc(lop.ClassCode, lop.Name, lop.LevelId, giaoVienId, lop.Capacity, lop.StartDate,
                lop.EndDate, lop.DayOfWeek, new TimeOnly(11, 0), new TimeOnly(11, 50), ClassStatus.Active));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(new TimeOnly(11, 0),
            await db.Sessions.Where(x => x.Id == saved.Id).Select(x => x.StartTime).SingleAsync());
        using var resync = await admin.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.NoContent, resync.StatusCode);
        var rows = await db.Sessions.AsNoTracking().Where(x => x.ClassId == lop.Id).ToListAsync();
        Assert.Equal(2, rows.Count);
        var updated = Assert.Single(rows, x => x.IsRegular == true);
        Assert.Equal(saved.Id, updated.Id);
        Assert.Equal(new TimeOnly(11, 0), updated.StartTime);
        Assert.Equal(new TimeOnly(8, 0), Assert.Single(rows, x => x.IsRegular == false).StartTime);
    }

    [KiemThuSqlServer]
    public async Task MoLopXepBuoiHangTuanTheoThuGioVaChoPhepHocBu()
    {
        await using var ungDung = new AuthenticationApiTests.AuthenticationTestApp();
        await ungDung.InitializeAsync();
        var (capDoId, _, _, giaoVienId) = await TaoDuLieuNen(ungDung);
        using var admin = await DangNhap(ungDung, AuthenticationApiTests.AuthenticationTestApp.Email,
            AuthenticationApiTests.AuthenticationTestApp.Password);
        using var scope = ungDung.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var baiHai = new Lesson { LevelId = capDoId, Code = "BH2", Name = "Bài 2", SortOrder = 2 };
        var baiMot = new Lesson { LevelId = capDoId, Code = "BH1", Name = "Bài 1", SortOrder = 1 };
        db.Lessons.AddRange(baiHai, baiMot);
        await db.SaveChangesAsync();

        var ngayBatDau = new DateOnly(2026, 11, 20);
        var gioBatDau = new TimeOnly(10, 0);
        var gioKetThuc = new TimeOnly(10, 50);
        var lopChuNhat = new YeuCauLopHoc("AUTO", "Lớp Chủ nhật", capDoId, giaoVienId, 10,
            ngayBatDau, new DateOnly(2026, 12, 6), DayOfWeek.Sunday, gioBatDau, gioKetThuc, ClassStatus.Active);

        async Task<ThongTinLopHoc> TaoLop(YeuCauLopHoc yeuCau)
        {
            using var response = await admin.PostAsJsonAsync("/api/classes", yeuCau);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<ThongTinLopHoc>())!;
        }
        async Task<List<PhanHoiBuoiHoc>> LayBuoi(int classId) =>
            (await admin.GetFromJsonAsync<PagedResult<PhanHoiBuoiHoc>>($"/api/sessions?classId={classId}"))!.Items.ToList();

        var lop = await TaoLop(lopChuNhat);
        var buoi = await LayBuoi(lop.Id);
        Assert.Equal(new[] { new DateOnly(2026, 11, 22), new DateOnly(2026, 11, 29), new DateOnly(2026, 12, 6) },
            buoi.Select(x => x.SessionDate));
        Assert.All(buoi, x =>
        {
            Assert.Equal(gioBatDau, x.StartTime);
            Assert.Equal(gioKetThuc, x.EndTime);
            Assert.Equal(SessionStatus.Scheduled, x.Status);
        });
        Assert.Equal(new int?[] { baiMot.Id, baiHai.Id, null }, buoi.Select(x => x.LessonId));

        using var bu = await admin.PostAsJsonAsync("/api/sessions",
            new YeuCauTaoBuoiHoc(lop.Id, baiHai.Id, new DateOnly(2026, 12, 5), gioBatDau, gioKetThuc, "Học bù"));
        Assert.Equal(HttpStatusCode.Created, bu.StatusCode);
        Assert.Equal(4, (await LayBuoi(lop.Id)).Count);

        var lopChuanBi = lopChuNhat with { TenLop = "Lớp Thứ ba", Thu = DayOfWeek.Tuesday, NgayKetThuc = null, TrangThai = ClassStatus.Preparing };
        var lopThuBa = await TaoLop(lopChuanBi);
        Assert.Empty(await LayBuoi(lopThuBa.Id));
        var lopDangHoc = lopChuanBi with { TrangThai = ClassStatus.Active };
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/classes/{lopThuBa.Id}", lopDangHoc)).StatusCode);
        Assert.Equal(new[] { new DateOnly(2026, 11, 24), new DateOnly(2026, 12, 1) },
            (await LayBuoi(lopThuBa.Id)).Select(x => x.SessionDate));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/classes/{lopThuBa.Id}", lopDangHoc)).StatusCode);
        Assert.Equal(2, (await LayBuoi(lopThuBa.Id)).Count);
    }

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
