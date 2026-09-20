using System.Data;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Persistence.Repositories;

public class KhoDuLieuQuanLyLop(AppDbContext duLieu) :
    IKhoDuLieuLopHoc, IKhoDuLieuGhiDanh, IKhoDuLieuNhanXet
{
    public Task<List<Class>> LayLopAsync(CancellationToken maHuy) =>
        duLieu.Classes.AsNoTracking().OrderBy(x => x.Id).ToListAsync(maHuy);

    public Task<Class?> TimLopAsync(int id, CancellationToken maHuy) =>
        duLieu.Classes.FirstOrDefaultAsync(x => x.Id == id, maHuy);

    public Task<int> DemSiSoAsync(int id, CancellationToken maHuy) =>
        duLieu.Enrollments.CountAsync(x => x.ClassId == id &&
            (x.Status == EnrollmentStatus.Active || x.Status == EnrollmentStatus.Paused), maHuy);

    public Task<bool> TrungMaAsync(string ma, int? boQuaId, CancellationToken maHuy) =>
        duLieu.Classes.AnyAsync(x => x.ClassCode == ma && x.Id != boQuaId, maHuy);

    public Task<bool> CapDoHoatDongAsync(int id, CancellationToken maHuy) =>
        duLieu.Levels.AnyAsync(x => x.Id == id && x.IsActive, maHuy);

    public Task<bool> GiaoVienHopLeAsync(string id, CancellationToken maHuy) =>
        (from u in duLieu.Users join ur in duLieu.UserRoles on u.Id equals ur.UserId
         join role in duLieu.Roles on ur.RoleId equals role.Id
         where u.Id == id && role.Name == UserRole.Teacher &&
               u.EmploymentStatus == EmploymentStatus.Active select u.Id).AnyAsync(maHuy);

    public Task<bool> TrungLichAsync(string giaoVienId, int? boQuaId, DayOfWeek thu,
        TimeOnly batDau, TimeOnly ketThuc, DateOnly ngayBatDau, DateOnly? ngayKetThuc,
        CancellationToken maHuy) => duLieu.Classes.AnyAsync(x => x.Id != boQuaId &&
            x.MainTeacherUserId == giaoVienId && x.Status != ClassStatus.Cancelled &&
            x.Status != ClassStatus.Completed && x.DayOfWeek == thu &&
            x.StartTime < ketThuc && batDau < x.EndTime &&
            (x.EndDate == null || x.EndDate >= ngayBatDau) &&
            (ngayKetThuc == null || x.StartDate <= ngayKetThuc), maHuy);

    public async Task<bool> CoDuLieuLienQuanAsync(int id, CancellationToken maHuy) =>
        await duLieu.Enrollments.AnyAsync(x => x.ClassId == id, maHuy) ||
        await duLieu.Sessions.AnyAsync(x => x.ClassId == id, maHuy);

    public void Them(Class lop) => duLieu.Classes.Add(lop);
    public void Xoa(Class lop) => duLieu.Classes.Remove(lop);
    public void Them(Enrollment ghiDanh) => duLieu.Enrollments.Add(ghiDanh);
    public void Them(StudentRemark nhanXet) => duLieu.StudentRemarks.Add(nhanXet);
    public Task LuuAsync(CancellationToken maHuy) => duLieu.SaveChangesAsync(maHuy);

    public async Task<T> TrongGiaoDichAsync<T>(Func<Task<T>> congViec, CancellationToken maHuy)
    {
        await using var giaoDich = await duLieu.Database.BeginTransactionAsync(IsolationLevel.Serializable, maHuy);
        var ketQua = await congViec();
        await giaoDich.CommitAsync(maHuy);
        return ketQua;
    }

    public Task<List<Enrollment>> LayDanhSachAsync(int? lopId, int? hocVienId, CancellationToken maHuy)
    {
        var truyVan = duLieu.Enrollments.AsNoTracking().AsQueryable();
        if (lopId.HasValue) truyVan = truyVan.Where(x => x.ClassId == lopId);
        if (hocVienId.HasValue) truyVan = truyVan.Where(x => x.StudentId == hocVienId);
        return truyVan.OrderByDescending(x => x.Id).ToListAsync(maHuy);
    }

    public Task<Enrollment?> TimAsync(int id, CancellationToken maHuy) =>
        duLieu.Enrollments.Include(x => x.Class).FirstOrDefaultAsync(x => x.Id == id, maHuy);

    public Task<Student?> TimHocVienAsync(int id, CancellationToken maHuy) =>
        duLieu.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, maHuy);

    Task<Class?> IKhoDuLieuGhiDanh.TimLopAsync(int id, CancellationToken maHuy) =>
        duLieu.Classes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, maHuy);

    public Task<bool> DaGhiDanhAsync(int hocVienId, CancellationToken maHuy) =>
        duLieu.Enrollments.AnyAsync(x => x.StudentId == hocVienId &&
            (x.Status == EnrollmentStatus.Active || x.Status == EnrollmentStatus.Paused), maHuy);

    public async Task<PagedResult<StudentRemark>> LayDanhSachAsync(int ghiDanhId, int trang,
        int kichThuocTrang, CancellationToken maHuy)
    {
        var truyVan = duLieu.StudentRemarks.AsNoTracking().Where(x => x.EnrollmentId == ghiDanhId);
        var tongSo = await truyVan.CountAsync(maHuy);
        var danhSach = await truyVan.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((trang - 1) * kichThuocTrang).Take(kichThuocTrang).ToListAsync(maHuy);
        return new PagedResult<StudentRemark>(danhSach, trang, kichThuocTrang, tongSo);
    }

    Task<StudentRemark?> IKhoDuLieuNhanXet.TimAsync(int id, CancellationToken maHuy) =>
        duLieu.StudentRemarks.FirstOrDefaultAsync(x => x.Id == id, maHuy);

    public Task<Enrollment?> TimGhiDanhAsync(int id, CancellationToken maHuy) =>
        duLieu.Enrollments.Include(x => x.Class).FirstOrDefaultAsync(x => x.Id == id, maHuy);

    public Task<Session?> TimBuoiHocAsync(int id, CancellationToken maHuy) =>
        duLieu.Sessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, maHuy);
}
