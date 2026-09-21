using CmsEdu.Application.Classes;
using CmsEdu.Application.Enrollments;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using CmsEdu.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.UnitTests;

public class AuthorizationClassEnrollmentTests
{
    private sealed record User(string? Role, string? UserId = "teacher", bool IsAuthenticated = true) : ICurrentUser;
    private static AppDbContext Database()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var own = new Class { Id = 1, MainTeacherUserId = "teacher" };
        db.Classes.AddRange(own, new Class { Id = 2, MainTeacherUserId = "other" });
        db.Enrollments.Add(new Enrollment { Id = 1, ClassId = 1, Class = own,
            PauseReason = "private pause", EndReason = "private end" });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task TeacherScopeUsesCurrentAssignment()
    {
        await using var db = Database();
        var repo = new KhoDuLieuQuanLyLop(db);
        var classes = new DichVuLopHoc(repo, new User(UserRole.Teacher));
        var enrollments = new DichVuGhiDanh(repo, new User(UserRole.Teacher));
        Assert.Equal(1, Assert.Single(await classes.DanhSach(default)).Id);
        Assert.Equal(1, (await classes.ChiTiet(1, default)).Id);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => classes.ChiTiet(2, default));
        Assert.Equal(1, (await enrollments.ChiTiet(1, default)).Id);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => enrollments.DanhSach(1, null, default));
        (await db.Classes.FindAsync(1))!.MainTeacherUserId = "other";
        await db.SaveChangesAsync();
        Assert.Empty(await classes.DanhSach(default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => classes.ChiTiet(1, default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => enrollments.ChiTiet(1, default));
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Accountant)]
    [InlineData(UserRole.CustomerCare)]
    public async Task ReadersCanReadAndAccountantGetsNoFreeText(string role)
    {
        await using var db = Database();
        var repo = new KhoDuLieuQuanLyLop(db);
        Assert.Equal(2, (await new DichVuLopHoc(repo, new User(role)).DanhSach(default)).Count);
        var service = new DichVuGhiDanh(repo, new User(role));
        var list = Assert.Single(await service.DanhSach(null, null, default));
        var detail = await service.ChiTiet(1, default);
        Assert.Equal(role == UserRole.Accountant ? null : "private pause", list.LyDoBaoLuu);
        Assert.Equal(role == UserRole.Accountant ? null : "private end", detail.LyDoKetThuc);
    }

    [Theory]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Accountant)]
    [InlineData(UserRole.CustomerCare)]
    [InlineData("Unknown")]
    public async Task NonAdminCannotDeleteClassOrCompleteEnrollment(string role)
    {
        await using var db = Database();
        var repo = new KhoDuLieuQuanLyLop(db);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new DichVuLopHoc(repo, new User(role)).Xoa(1, default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new DichVuGhiDanh(repo, new User(role))
            .HoanThanh(1, new KetThucYeuCauGhiDanh("done", DateOnly.FromDateTime(DateTime.Today)), default));
    }

    [Theory]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Accountant)]
    public async Task ReadOnlyRolesCannotChangeEnrollment(string role)
    {
        await using var db = Database();
        var service = new DichVuGhiDanh(new KhoDuLieuQuanLyLop(db), new User(role));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.BaoLuu(1, new YeuCauBaoLuu("pause", default), default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.TroLai(1, default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.NghiHoc(1, new KetThucYeuCauGhiDanh("end", default), default));
    }

    [Theory]
    [InlineData("Unknown", "user", true)]
    [InlineData(UserRole.Teacher, null, true)]
    [InlineData(UserRole.Admin, "user", false)]
    public async Task InvalidIdentityIsDenied(string role, string? id, bool authenticated)
    {
        await using var db = Database();
        var repo = new KhoDuLieuQuanLyLop(db);
        var user = new User(role, id, authenticated);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new DichVuLopHoc(repo, user).DanhSach(default));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => new DichVuGhiDanh(repo, user).ChiTiet(1, default));
    }
}
