using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CmsEdu.Api.Controllers;
using CmsEdu.Application.Students;
using CmsEdu.Application.Common.Models;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace CmsEdu.IntegrationTests;

public sealed class KiemThuSqlServerAttribute : FactAttribute
{
    public KiemThuSqlServerAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CMSEDU_TEST_SQLSERVER")))
            Skip = "Đặt CMSEDU_TEST_SQLSERVER bằng chuỗi kết nối SQL Server; kiểm thử sẽ tạo CSDL riêng.";
    }
}

public class KiemThuApiHocVien
{
    internal sealed class UngDungKiemThu : WebApplicationFactory<HocVienController>
    {
        public const string KhoaKy = "CmsEdu-Integration-Tests-Only-Signing-Key-2026";
        private readonly string tenCoSoDuLieu = $"CmsEduStudentTests_{Guid.NewGuid():N}";
        public string ConnectionString { get; }
        public UngDungKiemThu()
        {
            var ketNoi = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CMSEDU_TEST_SQLSERVER"));
            ketNoi.InitialCatalog = tenCoSoDuLieu;
            ConnectionString = ketNoi.ConnectionString;
        }
        protected override void ConfigureWebHost(IWebHostBuilder boDung)
        {
            boDung.UseEnvironment("Testing");
            boDung.UseSetting("Jwt:SigningKey", KhoaKy);
            boDung.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        }
        public async Task KhoiTaoAsync()
        {
            using var phamVi = Services.CreateScope();
            await phamVi.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }
        public HttpClient TaoTrinhKhach(string? vaiTro = "Admin")
        {
            var trinhKhach = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            if (vaiTro is not null)
            {
                var maTruyCap = new JwtSecurityToken("CmsEdu.Api", "CmsEdu.Web",
                    [new Claim(JwtRegisteredClaimNames.Sub, "test-user"), new Claim(ClaimTypes.Role, vaiTro)],
                    expires: DateTime.UtcNow.AddMinutes(5),
                    signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(KhoaKy)), SecurityAlgorithms.HmacSha256));
                trinhKhach.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(maTruyCap));
            }
            return trinhKhach;
        }
        public override async ValueTask DisposeAsync()
        {
            // Chỉ xóa CSDL riêng do bộ kiểm thử tạo sau khi xác minh đúng tên.
            if (!tenCoSoDuLieu.StartsWith("CmsEduStudentTests_", StringComparison.Ordinal) ||
                new SqlConnectionStringBuilder(ConnectionString).InitialCatalog != tenCoSoDuLieu)
                throw new InvalidOperationException("Tên cơ sở dữ liệu kiểm thử không đúng dự kiến.");
            var tuyChon = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
            await using var nguCanh = new AppDbContext(tuyChon);
            await nguCanh.Database.EnsureDeletedAsync();
            await base.DisposeAsync();
        }
    }

    private static YeuCauTaoHocVien TaoYeuCau(string maHocVien = "HV001") => new(maHocVien, "Nguyễn An", new DateOnly(2019, 5, 10), null, "Lưu ý học tập");

    [KiemThuSqlServer]
    public async Task TaoTimKiemCapNhatLuuTruVaGhiNhatKy()
    {
        await using var ungDung = new UngDungKiemThu();
        await ungDung.KhoiTaoAsync();
        using var trinhKhach = ungDung.TaoTrinhKhach();
        var phanHoiTao = await trinhKhach.PostAsJsonAsync("/api/students", new
        {
            studentCode = "HV001", fullName = "Nguyễn An", dateOfBirth = "2019-05-10",
            gender = (int?)null, learningNote = "Lưu ý học tập"
        });
        Assert.Equal(HttpStatusCode.Created, phanHoiTao.StatusCode);
        var hocVien = (await phanHoiTao.Content.ReadFromJsonAsync<PhanHoiHocVien>())!;
        Assert.NotNull(phanHoiTao.Headers.Location);
        using var duLieuJson = System.Text.Json.JsonDocument.Parse(await phanHoiTao.Content.ReadAsStringAsync());
        Assert.Equal("HV001", duLieuJson.RootElement.GetProperty("studentCode").GetString());
        Assert.Equal("Nguyễn An", duLieuJson.RootElement.GetProperty("fullName").GetString());
        Assert.False(duLieuJson.RootElement.GetProperty("isArchived").GetBoolean());
        Assert.Empty((await trinhKhach.GetFromJsonAsync<PagedResult<PhanHoiHocVien>>("/api/students?search=KHONG_TON_TAI"))!.Items);
        Assert.Equal("Nguyễn An", (await trinhKhach.GetFromJsonAsync<PhanHoiHocVien>(phanHoiTao.Headers.Location))!.FullName);
        var danhSach = await trinhKhach.GetFromJsonAsync<PagedResult<PhanHoiHocVien>>("/api/students?search=HV001&pageSize=1");
        Assert.Single(danhSach!.Items);
        var phanHoiCapNhat = await trinhKhach.PutAsJsonAsync($"/api/students/{hocVien.Id}", new YeuCauCapNhatHocVien("HV001", "Tên mới", new(2018, 1, 2), null, null));
        Assert.Equal(HttpStatusCode.OK, phanHoiCapNhat.StatusCode);
        Assert.Equal("Tên mới", (await trinhKhach.GetFromJsonAsync<PhanHoiHocVien>($"/api/students/{hocVien.Id}"))!.FullName);
        Assert.Equal(HttpStatusCode.Conflict, (await trinhKhach.PostAsJsonAsync("/api/students", TaoYeuCau())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await trinhKhach.PostAsync($"/api/students/{hocVien.Id}/archive", null)).StatusCode);
        Assert.Empty((await trinhKhach.GetFromJsonAsync<PagedResult<PhanHoiHocVien>>("/api/students"))!.Items);
        Assert.Single((await trinhKhach.GetFromJsonAsync<PagedResult<PhanHoiHocVien>>("/api/students?isArchived=true"))!.Items);
        Assert.Equal(HttpStatusCode.NoContent, (await trinhKhach.PostAsync($"/api/students/{hocVien.Id}/archive", null)).StatusCode);
        using var phamVi = ungDung.Services.CreateScope();
        Assert.Equal(3, await phamVi.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.CountAsync());
    }

    [KiemThuSqlServer]
    public async Task KhoiPhucHoSoVaTraCuuTheoTrangThai()
    {
        await using var ungDung = new UngDungKiemThu();
        await ungDung.KhoiTaoAsync();
        using var quanTriVien = ungDung.TaoTrinhKhach();
        using var chamSocKhachHang = ungDung.TaoTrinhKhach("CustomerCare");
        var phanHoiTao = await quanTriVien.PostAsJsonAsync("/api/students", TaoYeuCau());
        var hocVien = (await phanHoiTao.Content.ReadFromJsonAsync<PhanHoiHocVien>())!;
        var duongDan = $"/api/students/{hocVien.Id}";
        Assert.Equal(HttpStatusCode.NoContent, (await quanTriVien.PostAsync($"{duongDan}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await quanTriVien.PutAsJsonAsync(duongDan,
            new YeuCauCapNhatHocVien("HV001", "Tên thay đổi", new(2019, 5, 10), null, null))).StatusCode);
        var daLuuTru = await quanTriVien.GetFromJsonAsync<PagedResult<PhanHoiHocVien>>("/api/students?search=HV001&isArchived=true");
        Assert.True(Assert.Single(daLuuTru!.Items).IsArchived);
        Assert.Equal(HttpStatusCode.Forbidden, (await chamSocKhachHang.PostAsync($"{duongDan}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await quanTriVien.PostAsync($"{duongDan}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await quanTriVien.PostAsync($"{duongDan}/restore", null)).StatusCode);
        Assert.False((await quanTriVien.GetFromJsonAsync<PhanHoiHocVien>(duongDan))!.IsArchived);
        Assert.Single((await quanTriVien.GetFromJsonAsync<PagedResult<PhanHoiHocVien>>("/api/students?search=" + Uri.EscapeDataString(" Nguyễn An ")) )!.Items);
        Assert.Empty((await quanTriVien.GetFromJsonAsync<PagedResult<PhanHoiHocVien>>("/api/students?isArchived=true"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await quanTriVien.PostAsync("/api/students/9999/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await quanTriVien.PutAsJsonAsync(duongDan,
            new YeuCauCapNhatHocVien("HV001", "Tên thay đổi", new(2019, 5, 10), null, null))).StatusCode);
        using var phamVi = ungDung.Services.CreateScope();
        Assert.Equal(1, await phamVi.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .CountAsync(nhatKy => nhatKy.Action == "Student.Restore"));
    }

    [KiemThuSqlServer]
    public async Task KiemTraDuLieuVaPhanQuyenTraDungMaHttp()
    {
        await using var ungDung = new UngDungKiemThu();
        await ungDung.KhoiTaoAsync();
        using var quanTriVien = ungDung.TaoTrinhKhach();
        using var khachChuaDangNhap = ungDung.TaoTrinhKhach(null);
        using var giaoVien = ungDung.TaoTrinhKhach("Teacher");
        using var keToan = ungDung.TaoTrinhKhach("Accountant");
        using var chamSocKhachHang = ungDung.TaoTrinhKhach("CustomerCare");
        Assert.Equal(HttpStatusCode.Unauthorized, (await khachChuaDangNhap.GetAsync("/api/students")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await giaoVien.PostAsJsonAsync("/api/students", TaoYeuCau())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await quanTriVien.PostAsJsonAsync("/api/students", TaoYeuCau() with { FullName = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await quanTriVien.PostAsJsonAsync("/api/students", TaoYeuCau() with { StudentCode = new string('X', 51) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await quanTriVien.PostAsJsonAsync("/api/students", TaoYeuCau() with { DateOfBirth = DateOnly.MaxValue })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await quanTriVien.PostAsJsonAsync("/api/students", TaoYeuCau() with { Gender = (CmsEdu.Domain.Enums.Gender)99 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await quanTriVien.GetAsync("/api/students?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await quanTriVien.GetAsync("/api/students/9999")).StatusCode);
        var phanHoi = await chamSocKhachHang.PostAsJsonAsync("/api/students", TaoYeuCau());
        Assert.Equal(HttpStatusCode.Created, phanHoi.StatusCode);
        var hocVien = (await phanHoi.Content.ReadFromJsonAsync<PhanHoiHocVien>())!;
        Assert.Equal(HttpStatusCode.Forbidden, (await chamSocKhachHang.PostAsync($"/api/students/{hocVien.Id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await giaoVien.GetAsync($"/api/students/{hocVien.Id}")).StatusCode);
        Assert.Empty((await giaoVien.GetFromJsonAsync<PagedResult<PhanHoiHocVien>>("/api/students"))!.Items);
        Assert.Null((await keToan.GetFromJsonAsync<PhanHoiHocVien>($"/api/students/{hocVien.Id}"))!.LearningNote);
    }
    [KiemThuSqlServer]
    public async Task PhamViGiaoVienVaLuuTruTuanThuQuyTacGhiDanh()
    {
        await using var ungDung = new UngDungKiemThu();
        await ungDung.KhoiTaoAsync();
        using var quanTriVien = ungDung.TaoTrinhKhach();
        using var giaoVien = ungDung.TaoTrinhKhach("Teacher");
        var phanHoiTao = await quanTriVien.PostAsJsonAsync("/api/students", TaoYeuCau());
        var hocVien = (await phanHoiTao.Content.ReadFromJsonAsync<PhanHoiHocVien>())!;
        using var phamVi = ungDung.Services.CreateScope();
        var coSoDuLieu = phamVi.ServiceProvider.GetRequiredService<AppDbContext>();
        coSoDuLieu.Users.Add(new CmsEdu.Infrastructure.Identity.ApplicationUser
        {
            Id = "test-user", UserName = "teacher", EmployeeCode = "GV001", FullName = "Giáo viên kiểm thử"
        });
        var ghiDanh = new CmsEdu.Domain.Entities.Enrollment
        {
            StudentId = hocVien.Id, StartDate = new(2026, 1, 1),
            Class = new CmsEdu.Domain.Entities.Class
            {
                ClassCode = "C001", Name = "Lớp kiểm thử", MainTeacherUserId = "test-user", Capacity = 10,
                StartDate = new(2026, 1, 1), StartTime = new(8, 0), EndTime = new(9, 0),
                Level = new CmsEdu.Domain.Entities.Level
                {
                    Code = "L001", Name = "Cấp độ kiểm thử", SortOrder = 1,
                    Course = new CmsEdu.Domain.Entities.Course { Code = "CO001", Name = "Khóa học kiểm thử" }
                }
            }
        };
        coSoDuLieu.Enrollments.Add(ghiDanh);
        await coSoDuLieu.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await giaoVien.GetAsync($"/api/students/{hocVien.Id}")).StatusCode);
        Assert.Single((await giaoVien.GetFromJsonAsync<PagedResult<PhanHoiHocVien>>("/api/students"))!.Items);
        Assert.Equal(HttpStatusCode.Conflict, (await quanTriVien.PostAsync($"/api/students/{hocVien.Id}/archive", null)).StatusCode);
        ghiDanh.Status = CmsEdu.Domain.Enums.EnrollmentStatus.Paused;
        ghiDanh.PauseReason = "Bảo lưu để kiểm thử";
        await coSoDuLieu.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await quanTriVien.PostAsync($"/api/students/{hocVien.Id}/archive", null)).StatusCode);
        ghiDanh.Status = CmsEdu.Domain.Enums.EnrollmentStatus.Completed;
        ghiDanh.EndDate = new(2026, 6, 1);
        await coSoDuLieu.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await quanTriVien.PostAsync($"/api/students/{hocVien.Id}/archive", null)).StatusCode);
        Assert.True(await coSoDuLieu.Students.AsNoTracking().Where(hocVienTrongTruyVan => hocVienTrongTruyVan.Id == hocVien.Id).Select(hocVienTrongTruyVan => hocVienTrongTruyVan.IsArchived).SingleAsync());
    }

}
