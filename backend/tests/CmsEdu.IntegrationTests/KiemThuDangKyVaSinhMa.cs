using CmsEdu.Application.Attendances;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Identity;
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
    public async Task DiemDanhSql_TruBuoiMotLan_BaoLuuVaHetBuoiBiChan()
    {
        await using var app = new UngDungKiemThu();
        await app.KhoiTaoAsync();
        using var client = app.TaoTrinhKhach();
        int studentId, enrollmentId, sessionId;
        using (var scope = app.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var teacher = new ApplicationUser { UserName = "teacher", EmployeeCode = "GVTEST", FullName = "Giáo viên" };
            db.Users.Add(teacher);
            var student = new Student { StudentCode = "HSTEST", FullName = "Học viên", CourseMonths = 1, RemainingSessions = 1, DateOfBirth = new(2018, 1, 1) };
            var course = new Course { Code = "KHTEST", Name = "Khóa học" };
            var level = new Level { Course = course, Code = "CDTEST", Name = "Cấp độ", SortOrder = 1 };
            var lop = new Class { ClassCode = "LHTEST", Name = "Lớp học", Level = level, MainTeacherUserId = teacher.Id,
                Capacity = 10, StartDate = new(2026, 1, 1), StartTime = new(8, 0), EndTime = new(9, 0), Status = ClassStatus.Active };
            var enrollment = new Enrollment { Student = student, Class = lop, StartDate = new(2026, 1, 1), Status = EnrollmentStatus.Active };
            var session = new Session { Class = lop, SessionDate = new(2026, 9, 1), StartTime = new(8, 0), EndTime = new(9, 0) };
            db.Enrollments.Add(enrollment); db.Sessions.Add(session);
            await db.SaveChangesAsync();
            studentId = student.Id; enrollmentId = enrollment.Id; sessionId = session.Id;
        }
        var request = new YeuCauLuuDiemDanhBuoiHoc([new(enrollmentId, AttendanceStatus.Absent, null)]);
        var saved = await client.PutAsJsonAsync($"/api/sessions/{sessionId}/attendance", request);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<PhanHoiHocVien>($"/api/students/{studentId}"))!.RemainingSessions);
        var edited = await client.PutAsJsonAsync($"/api/sessions/{sessionId}/attendance", new YeuCauLuuDiemDanhBuoiHoc([new(enrollmentId, AttendanceStatus.Present, null)]));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<PhanHoiHocVien>($"/api/students/{studentId}"))!.RemainingSessions);
        int nextId;
        using (var scope = app.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var previous = await db.Sessions.SingleAsync();
            var next = new Session { ClassId = previous.ClassId, SessionDate = new(2026, 9, 8), StartTime = new(8, 0), EndTime = new(9, 0) };
            db.Sessions.Add(next); await db.SaveChangesAsync(); nextId = next.Id;
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/sessions/{nextId}/attendance", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/students/{studentId}/renew", new YeuCauGiaHan(1))).StatusCode);
        using (var scope = app.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Enrollments.SingleAsync()).Status = EnrollmentStatus.Paused;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/sessions/{nextId}/attendance", request)).StatusCode);
        Assert.Equal(4, (await client.GetFromJsonAsync<PhanHoiHocVien>($"/api/students/{studentId}"))!.RemainingSessions);
    }

    [KiemThuSqlServer]
    public async Task ThoiHanVaGiaHan_QuyDoiBonBuoiMoiThang_KiemTraQuyen()
    {
        await using var app = new UngDungKiemThu();
        await app.KhoiTaoAsync();
        using var client = app.TaoTrinhKhach();
        var request = new YeuCauDangKyHocVien("Học viên gói học", new(2018, 1, 1), null, null, null,
            new YeuCauNguoiGiamHo("Phụ huynh", "0901234567", null), "Mẹ", 6);
        var invalid = await client.PostAsJsonAsync("/api/students/register", request with { CourseMonths = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var created = await client.PostAsJsonAsync("/api/students/register", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var student = (await created.Content.ReadFromJsonAsync<PhanHoiHocVien>())!;
        Assert.Equal(24, student.RemainingSessions);
        var renewed = await client.PostAsJsonAsync($"/api/students/{student.Id}/renew", new YeuCauGiaHan(3));
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        var result = (await renewed.Content.ReadFromJsonAsync<PhanHoiHocVien>())!;
        Assert.Equal(9, result.CourseMonths);
        Assert.Equal(36, result.RemainingSessions);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/students/{student.Id}/renew", new YeuCauGiaHan(-1))).StatusCode);
        using var teacher = app.TaoTrinhKhach("Teacher");
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsJsonAsync($"/api/students/{student.Id}/renew", new YeuCauGiaHan(1))).StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(36, (await db.Students.SingleAsync()).RemainingSessions);
        Assert.Single(await db.AuditLogs.Where(a => a.Action == "Student.Renew").ToListAsync());
    }

    [KiemThuSqlServer]
    public async Task DangKyDungChungNguoiGiamHoVaHoanTacKhiLoi()
    {
        await using var ungDung = new UngDungKiemThu();
        await ungDung.KhoiTaoAsync();
        using var khach = ungDung.TaoTrinhKhach();
        var yeuCau = new YeuCauDangKyHocVien("Học viên một", new(2018, 1, 1), null, null, null,
            new YeuCauNguoiGiamHo("Người giám hộ", "0912345678", null), "Mẹ", 3);
        var phanHoi = await khach.PostAsJsonAsync("/api/students/register", yeuCau);
        Assert.Equal(HttpStatusCode.Created, phanHoi.StatusCode);
        var dau = (await phanHoi.Content.ReadFromJsonAsync<PhanHoiHocVien>())!;
        Assert.Equal("HS0001", dau.StudentCode);
        Assert.Equal(3, dau.CourseMonths);
        Assert.Equal(12, dau.RemainingSessions);
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
