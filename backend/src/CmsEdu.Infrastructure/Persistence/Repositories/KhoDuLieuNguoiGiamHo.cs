using System.Data;
using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Persistence.Repositories;

public sealed class KhoDuLieuNguoiGiamHo(AppDbContext nguCanh) : IKhoDuLieuNguoiGiamHo
{
    public async Task<PagedResult<Guardian>> LayDanhSachAsync(string? tuKhoa, bool? dangHoatDong, int trang, int kichThuocTrang, CancellationToken maHuy)
    {
        var truyVan = nguCanh.Guardians.AsNoTracking();
        if (dangHoatDong.HasValue) truyVan = truyVan.Where(ngh => ngh.IsActive == dangHoatDong.Value);
        if (!string.IsNullOrWhiteSpace(tuKhoa)) truyVan = truyVan.Where(ngh => ngh.FullName.Contains(tuKhoa) || ngh.Phone.Contains(tuKhoa) || (ngh.Email != null && ngh.Email.Contains(tuKhoa)));
        var tongSo = await truyVan.CountAsync(maHuy);
        return new(await truyVan.OrderBy(ngh => ngh.Id).Skip((trang - 1) * kichThuocTrang).Take(kichThuocTrang).ToListAsync(maHuy), trang, kichThuocTrang, tongSo);
    }

    public Task<Guardian?> TimTheoIdAsync(int maNguoiGiamHo, CancellationToken maHuy) =>
        nguCanh.Guardians.AsNoTracking().SingleOrDefaultAsync(ngh => ngh.Id == maNguoiGiamHo, maHuy);

    // Khóa cập nhật trên hồ sơ để tuần tự hóa thao tác liên kết và thay đổi trạng thái.
    private async Task<Guardian> KhoaNguoiGiamHoAsync(int maNguoiGiamHo, CancellationToken maHuy) =>
        await nguCanh.Guardians.FromSqlInterpolated($"SELECT * FROM [Guardians] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {maNguoiGiamHo}")
            .SingleOrDefaultAsync(maHuy) ?? throw new NotFoundException("Không tìm thấy người giám hộ.");

    private async Task KhoaHocVienAsync(int maHocVien, CancellationToken maHuy)
    {
        var hocVien = await nguCanh.Students.FromSqlInterpolated($"SELECT * FROM [Students] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {maHocVien}")
            .SingleOrDefaultAsync(maHuy) ?? throw new NotFoundException("Không tìm thấy học viên.");
        if (hocVien.IsArchived) throw new ConflictException("Cần khôi phục học viên trước khi thay đổi liên kết người giám hộ.");
    }

    public Task LuuAsync(Guardian nguoiGiamHo, bool taoMoi, string? maNguoiDung, CancellationToken maHuy) => TrongGiaoDichAsync(async () =>
    {
        if (taoMoi) nguCanh.Guardians.Add(nguoiGiamHo);
        else
        {
            var hoSo = await KhoaNguoiGiamHoAsync(nguoiGiamHo.Id, maHuy);
            if (!nguoiGiamHo.IsActive && await nguCanh.StudentGuardians.AnyAsync(lk => lk.GuardianId == hoSo.Id && lk.IsPrimary, maHuy))
                throw new ConflictException("Cần chuyển hoặc gỡ vai trò giám hộ chính trước khi ngừng hoạt động.");
            hoSo.FullName = nguoiGiamHo.FullName;
            hoSo.Phone = nguoiGiamHo.Phone;
            hoSo.Email = nguoiGiamHo.Email;
            hoSo.IsActive = nguoiGiamHo.IsActive;
        }
        await nguCanh.SaveChangesAsync(maHuy);
        GhiNhatKy("Guardian", nguoiGiamHo.Id, taoMoi ? "Guardian.Create" : "Guardian.Update", maNguoiDung);
    }, maHuy);

    public Task XoaAsync(int maNguoiGiamHo, string? maNguoiDung, CancellationToken maHuy) => TrongGiaoDichAsync(async () =>
    {
        var nguoiGiamHo = await KhoaNguoiGiamHoAsync(maNguoiGiamHo, maHuy);
        if (await nguCanh.StudentGuardians.AnyAsync(lk => lk.GuardianId == maNguoiGiamHo, maHuy))
            throw new ConflictException("Không thể xóa người giám hộ đang liên kết với học viên. Hãy gỡ liên kết trước.");
        nguCanh.Guardians.Remove(nguoiGiamHo);
        GhiNhatKy("Guardian", maNguoiGiamHo, "Guardian.Delete", maNguoiDung);
    }, maHuy);

    public async Task<IReadOnlyList<StudentGuardian>> LayLienKetAsync(int maHocVien, string? maGiaoVien, CancellationToken maHuy)
    {
        if (!await nguCanh.Students.AnyAsync(hv => hv.Id == maHocVien && (maGiaoVien == null || hv.Enrollments.Any(gd => gd.Class.MainTeacherUserId == maGiaoVien)), maHuy))
            throw new NotFoundException("Không tìm thấy học viên trong phạm vi truy cập.");
        return await nguCanh.StudentGuardians.AsNoTracking().Include(lk => lk.Guardian)
            .Where(lk => lk.StudentId == maHocVien).OrderByDescending(lk => lk.IsPrimary).ThenBy(lk => lk.GuardianId).ToListAsync(maHuy);
    }

    public Task GanLienKetAsync(int maHocVien, int maNguoiGiamHo, string quanHe, bool laNguoiChinh, bool chiCapNhat, string? maNguoiDung, CancellationToken maHuy) => TrongGiaoDichAsync(async () =>
    {
        var nguoiGiamHo = await KhoaNguoiGiamHoAsync(maNguoiGiamHo, maHuy);
        await KhoaHocVienAsync(maHocVien, maHuy);
        if (!nguoiGiamHo.IsActive) throw new ConflictException("Người giám hộ đã ngừng hoạt động.");
        var danhSach = await nguCanh.StudentGuardians.Where(lk => lk.StudentId == maHocVien).ToListAsync(maHuy);
        var lienKet = danhSach.SingleOrDefault(lk => lk.GuardianId == maNguoiGiamHo);
        if (chiCapNhat && lienKet is null) throw new NotFoundException("Không tìm thấy liên kết người giám hộ.");
        // Gắn lại cập nhật quan hệ; isPrimary=false không tự gỡ vai trò chính hiện tại.
        var chonLamChinh = laNguoiChinh || !danhSach.Any(lk => lk.IsPrimary);
        if (chonLamChinh) await BoNguoiChinhCuAsync(danhSach, maNguoiGiamHo, maHuy);
        if (lienKet is null)
        {
            lienKet = new StudentGuardian { StudentId = maHocVien, GuardianId = maNguoiGiamHo };
            nguCanh.StudentGuardians.Add(lienKet);
        }
        lienKet.Relationship = quanHe;
        lienKet.IsPrimary = lienKet.IsPrimary || chonLamChinh;
        GhiNhatKy("Student", maHocVien, chiCapNhat ? "StudentGuardian.Update" : "StudentGuardian.Link", maNguoiDung, maNguoiGiamHo);
    }, maHuy);

    public Task DatNguoiChinhAsync(int maHocVien, int maNguoiGiamHo, string? maNguoiDung, CancellationToken maHuy) => TrongGiaoDichAsync(async () =>
    {
        var nguoiGiamHo = await KhoaNguoiGiamHoAsync(maNguoiGiamHo, maHuy);
        await KhoaHocVienAsync(maHocVien, maHuy);
        var danhSach = await nguCanh.StudentGuardians.Where(lk => lk.StudentId == maHocVien).ToListAsync(maHuy);
        var lienKet = danhSach.SingleOrDefault(lk => lk.GuardianId == maNguoiGiamHo)
            ?? throw new NotFoundException("Người giám hộ chưa liên kết với học viên.");
        if (!nguoiGiamHo.IsActive) throw new ConflictException("Không thể chọn người giám hộ đã ngừng hoạt động làm người chính.");
        if (lienKet.IsPrimary) return;
        await BoNguoiChinhCuAsync(danhSach, maNguoiGiamHo, maHuy);
        lienKet.IsPrimary = true;
        GhiNhatKy("Student", maHocVien, "StudentGuardian.SetPrimary", maNguoiDung, maNguoiGiamHo);
    }, maHuy);

    public Task GoLienKetAsync(int maHocVien, int maNguoiGiamHo, string? maNguoiDung, CancellationToken maHuy) => TrongGiaoDichAsync(async () =>
    {
        await KhoaNguoiGiamHoAsync(maNguoiGiamHo, maHuy);
        await KhoaHocVienAsync(maHocVien, maHuy);
        var danhSach = await nguCanh.StudentGuardians.Where(lk => lk.StudentId == maHocVien).ToListAsync(maHuy);
        var lienKet = danhSach.SingleOrDefault(lk => lk.GuardianId == maNguoiGiamHo)
            ?? throw new NotFoundException("Không tìm thấy liên kết người giám hộ.");
        if (lienKet.IsPrimary && danhSach.Count > 1)
            throw new ConflictException("Cần chọn người giám hộ chính khác trước khi gỡ liên kết này.");
        nguCanh.StudentGuardians.Remove(lienKet);
        GhiNhatKy("Student", maHocVien, "StudentGuardian.Unlink", maNguoiDung, maNguoiGiamHo);
    }, maHuy);

    private async Task BoNguoiChinhCuAsync(List<StudentGuardian> danhSach, int maNguoiGiamHo, CancellationToken maHuy)
    {
        foreach (var lienKet in danhSach.Where(lk => lk.IsPrimary && lk.GuardianId != maNguoiGiamHo)) lienKet.IsPrimary = false;
        // Lưu bước bỏ người cũ trước để không vi phạm unique index khi chọn người mới.
        // Cả hai bước nằm trong cùng giao dịch, lỗi sẽ hoàn tác toàn bộ.
        await nguCanh.SaveChangesAsync(maHuy);
    }

    private async Task TrongGiaoDichAsync(Func<Task> thaoTac, CancellationToken maHuy)
    {
        try
        {
            await using var giaoDich = await nguCanh.Database.BeginTransactionAsync(IsolationLevel.Serializable, maHuy);
            await thaoTac();
            await nguCanh.SaveChangesAsync(maHuy);
            await giaoDich.CommitAsync(maHuy);
        }
        catch (DbUpdateException ngoaiLe) when (ngoaiLe.InnerException is SqlException { Number: 2601 or 2627 or 547 or 1205 })
        {
            throw new ConflictException("Dữ liệu liên kết đã thay đổi hoặc đang được sử dụng. Hãy tải lại và thử lại.");
        }
        catch (SqlException ngoaiLe) when (ngoaiLe.Number == 1205)
        {
            throw new ConflictException("Có thao tác đồng thời trên hồ sơ. Hãy tải lại và thử lại.");
        }
    }

    private void GhiNhatKy(string loai, int maDinhDanh, string hanhDong, string? maNguoiDung, int? maNguoiGiamHo = null) =>
        nguCanh.AuditLogs.Add(new AuditLog
        {
            UserId = maNguoiDung, Action = hanhDong, EntityType = loai, EntityId = maDinhDanh.ToString(),
            Description = $"Thao tác {hanhDong} trên hồ sơ {maDinhDanh}" + (maNguoiGiamHo.HasValue ? $", người giám hộ {maNguoiGiamHo}." : "."),
            OccurredAt = DateTime.UtcNow
        });
}
