using System.Data;
using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace CmsEdu.Infrastructure.Persistence.Repositories;

public class KhoDuLieuHocVien(AppDbContext nguCanh) : IKhoDuLieuHocVien
{
    private IQueryable<Student> LocTheoPhamVi(string? maGiaoVien) => maGiaoVien is null ? nguCanh.Students :
        nguCanh.Students.Where(hocVienTrongTruyVan => hocVienTrongTruyVan.Enrollments.Any(ghiDanhTrongTruyVan => ghiDanhTrongTruyVan.Class.MainTeacherUserId == maGiaoVien));

    public async Task<PagedResult<Student>> LayDanhSachAsync(string? tuKhoa, bool? daLuuTru, string? maGiaoVien, int trang, int kichThuocTrang, CancellationToken maHuy)
    {
        var truyVan = LocTheoPhamVi(maGiaoVien).AsNoTracking();
        if (daLuuTru.HasValue) truyVan = truyVan.Where(hocVienTrongTruyVan => hocVienTrongTruyVan.IsArchived == daLuuTru.Value);
        if (!string.IsNullOrWhiteSpace(tuKhoa)) truyVan = truyVan.Where(hocVienTrongTruyVan => hocVienTrongTruyVan.StudentCode.Contains(tuKhoa) || hocVienTrongTruyVan.FullName.Contains(tuKhoa));
        var tongSo = await truyVan.CountAsync(maHuy);
        var danhSach = await truyVan.OrderBy(hocVienTrongTruyVan => hocVienTrongTruyVan.Id).Skip((trang - 1) * kichThuocTrang).Take(kichThuocTrang).ToListAsync(maHuy);
        return new(danhSach, trang, kichThuocTrang, tongSo);
    }

    public Task<Student?> TimTheoIdAsync(int maDinhDanh, string? maGiaoVien, CancellationToken maHuy) =>
        LocTheoPhamVi(maGiaoVien).SingleOrDefaultAsync(hocVienTrongTruyVan => hocVienTrongTruyVan.Id == maDinhDanh, maHuy);

    public Task<bool> MaDaTonTaiAsync(string maHocVien, int? maLoaiTru, CancellationToken maHuy) =>
        nguCanh.Students.AnyAsync(hocVienTrongTruyVan => hocVienTrongTruyVan.StudentCode == maHocVien && (!maLoaiTru.HasValue || hocVienTrongTruyVan.Id != maLoaiTru.Value), maHuy);

    public async Task LuuAsync(Student hocVien, bool laTaoMoi, string? maNguoiDung, CancellationToken maHuy)
    {
        await using var giaoDich = await nguCanh.Database.BeginTransactionAsync(maHuy);
        if (laTaoMoi) nguCanh.Students.Add(hocVien);
        try
        {
            await nguCanh.SaveChangesAsync(maHuy);
            GhiNhatKy(hocVien.Id, laTaoMoi ? "Student.Create" : "Student.Update", maNguoiDung);
            await nguCanh.SaveChangesAsync(maHuy);
            await giaoDich.CommitAsync(maHuy);
        }
        catch (DbUpdateException ngoaiLe) when (ngoaiLe.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("Mã học viên đã tồn tại.");
        }
    }

    public async Task LuuTruAsync(int maDinhDanh, string? maNguoiDung, CancellationToken maHuy)
    {
        await using var giaoDich = await nguCanh.Database.BeginTransactionAsync(IsolationLevel.Serializable, maHuy);
        var hocVien = await nguCanh.Students.SingleOrDefaultAsync(hocVienTrongTruyVan => hocVienTrongTruyVan.Id == maDinhDanh, maHuy)
            ?? throw new NotFoundException("Không tìm thấy học viên.");
        if (await nguCanh.Enrollments.AnyAsync(ghiDanhTrongTruyVan => ghiDanhTrongTruyVan.StudentId == maDinhDanh &&
            (ghiDanhTrongTruyVan.Status == EnrollmentStatus.Active || ghiDanhTrongTruyVan.Status == EnrollmentStatus.Paused), maHuy))
            throw new ConflictException("Không thể lưu trữ hồ sơ học viên đang học hoặc đang bảo lưu.");
        if (!hocVien.IsArchived)
        {
            hocVien.IsArchived = true;
            GhiNhatKy(maDinhDanh, "Student.Archive", maNguoiDung);
            await nguCanh.SaveChangesAsync(maHuy);
        }
        await giaoDich.CommitAsync(maHuy);
    }

    public async Task KhoiPhucAsync(int maDinhDanh, string? maNguoiDung, CancellationToken maHuy)
    {
        await using var giaoDich = await nguCanh.Database.BeginTransactionAsync(IsolationLevel.Serializable, maHuy);
        var hocVien = await nguCanh.Students.SingleOrDefaultAsync(hocVienTrongTruyVan => hocVienTrongTruyVan.Id == maDinhDanh, maHuy)
            ?? throw new NotFoundException("Không tìm thấy học viên.");
        if (hocVien.IsArchived)
        {
            hocVien.IsArchived = false;
            GhiNhatKy(maDinhDanh, "Student.Restore", maNguoiDung);
            await nguCanh.SaveChangesAsync(maHuy);
        }
        await giaoDich.CommitAsync(maHuy);
    }

    private void GhiNhatKy(int maDinhDanh, string hanhDong, string? maNguoiDung) => nguCanh.AuditLogs.Add(new AuditLog
    {
        UserId = maNguoiDung, Action = hanhDong, EntityType = nameof(Student), EntityId = maDinhDanh.ToString(),
        Description = hanhDong switch
        {
            "Student.Create" => $"Tạo hồ sơ học viên {maDinhDanh}.",
            "Student.Update" => $"Cập nhật hồ sơ học viên {maDinhDanh}.",
            "Student.Archive" => $"Lưu trữ hồ sơ học viên {maDinhDanh}.",
            "Student.Restore" => $"Khôi phục hồ sơ học viên {maDinhDanh}.",
            _ => $"Thay đổi hồ sơ học viên {maDinhDanh}."
        }, OccurredAt = DateTime.UtcNow
    });
}
