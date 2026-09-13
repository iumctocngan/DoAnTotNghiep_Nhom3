using System.ComponentModel.DataAnnotations;
using CmsEdu.Application.Common.Exceptions;
using ValidationException = CmsEdu.Application.Common.Exceptions.ValidationException;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;

namespace CmsEdu.Application.Guardians;

public sealed class DichVuNguoiGiamHo(IKhoDuLieuNguoiGiamHo khoDuLieu, ICurrentUser nguoiDung)
{
    private void KiemTraQuyenQuanLy(bool chiAdmin = false)
    {
        if (!nguoiDung.IsAuthenticated) throw new UnauthorizedAccessException();
        if (nguoiDung.Role != UserRole.Admin && (chiAdmin || nguoiDung.Role != UserRole.CustomerCare))
            throw new ForbiddenAccessException("Không có quyền quản lý người giám hộ.");
    }

    public async Task<PagedResult<PhanHoiNguoiGiamHo>> LayDanhSachAsync(string? tuKhoa, bool? dangHoatDong,
        int trang, int kichThuocTrang, CancellationToken maHuy = default)
    {
        KiemTraQuyenQuanLy();
        if (trang < 1 || kichThuocTrang < 1 || kichThuocTrang > 100 || (long)(trang - 1) * kichThuocTrang > int.MaxValue)
            throw new ValidationException("Số trang phải từ 1, kích thước trang từ 1 đến 100 và không vượt giới hạn phân trang.");
        var ketQua = await khoDuLieu.LayDanhSachAsync(tuKhoa?.Trim(), dangHoatDong, trang, kichThuocTrang, maHuy);
        return new(ketQua.Items.Select(TaoPhanHoi).ToList(), ketQua.Page, ketQua.PageSize, ketQua.TotalItems);
    }

    public async Task<PhanHoiNguoiGiamHo> LayTheoIdAsync(int maNguoiGiamHo, CancellationToken maHuy = default)
    {
        KiemTraQuyenQuanLy();
        return TaoPhanHoi(await khoDuLieu.TimTheoIdAsync(maNguoiGiamHo, maHuy)
            ?? throw new NotFoundException("Không tìm thấy người giám hộ."));
    }

    public async Task<PhanHoiNguoiGiamHo> LuuAsync(int? maNguoiGiamHo, YeuCauNguoiGiamHo yeuCau, CancellationToken maHuy = default)
    {
        KiemTraQuyenQuanLy();
        Validator.ValidateObject(yeuCau, new ValidationContext(yeuCau), true);
        var nguoiGiamHo = maNguoiGiamHo.HasValue
            ? await khoDuLieu.TimTheoIdAsync(maNguoiGiamHo.Value, maHuy) ?? throw new NotFoundException("Không tìm thấy người giám hộ.")
            : new Guardian();
        nguoiGiamHo.FullName = yeuCau.FullName.Trim();
        nguoiGiamHo.Phone = yeuCau.Phone.Trim();
        nguoiGiamHo.Email = string.IsNullOrWhiteSpace(yeuCau.Email) ? null : yeuCau.Email.Trim();
        nguoiGiamHo.IsActive = yeuCau.IsActive;
        await khoDuLieu.LuuAsync(nguoiGiamHo, !maNguoiGiamHo.HasValue, nguoiDung.UserId, maHuy);
        return TaoPhanHoi(nguoiGiamHo);
    }

    public Task XoaAsync(int maNguoiGiamHo, CancellationToken maHuy = default)
    {
        KiemTraQuyenQuanLy(true);
        return khoDuLieu.XoaAsync(maNguoiGiamHo, nguoiDung.UserId, maHuy);
    }

    public async Task<IReadOnlyList<PhanHoiLienKetNguoiGiamHo>> LayLienKetAsync(int maHocVien, CancellationToken maHuy = default)
    {
        if (!nguoiDung.IsAuthenticated) throw new UnauthorizedAccessException();
        var maGiaoVien = nguoiDung.Role switch
        {
            UserRole.Admin or UserRole.CustomerCare or UserRole.Accountant => null,
            UserRole.Teacher when !string.IsNullOrWhiteSpace(nguoiDung.UserId) => nguoiDung.UserId,
            _ => throw new ForbiddenAccessException("Không có quyền xem người giám hộ của học viên.")
        };
        var lienKet = await khoDuLieu.LayLienKetAsync(maHocVien, maGiaoVien, maHuy);
        return lienKet.Select(lk => new PhanHoiLienKetNguoiGiamHo(lk.GuardianId, lk.Guardian.FullName,
            lk.Guardian.Phone, lk.Guardian.Email, lk.Guardian.IsActive, lk.Relationship, lk.IsPrimary)).ToList();
    }

    public Task GanLienKetAsync(int maHocVien, YeuCauLienKetNguoiGiamHo yeuCau, CancellationToken maHuy = default)
    {
        KiemTraQuyenQuanLy();
        Validator.ValidateObject(yeuCau, new ValidationContext(yeuCau), true);
        return khoDuLieu.GanLienKetAsync(maHocVien, yeuCau.GuardianId, yeuCau.Relationship.Trim(), yeuCau.IsPrimary, false, nguoiDung.UserId, maHuy);
    }

    public Task CapNhatLienKetAsync(int maHocVien, int maNguoiGiamHo, YeuCauCapNhatLienKetNguoiGiamHo yeuCau, CancellationToken maHuy = default)
    {
        KiemTraQuyenQuanLy();
        var duLieu = new YeuCauLienKetNguoiGiamHo(maNguoiGiamHo, yeuCau.Relationship, yeuCau.IsPrimary);
        Validator.ValidateObject(duLieu, new ValidationContext(duLieu), true);
        return khoDuLieu.GanLienKetAsync(maHocVien, maNguoiGiamHo, yeuCau.Relationship.Trim(), yeuCau.IsPrimary, true, nguoiDung.UserId, maHuy);
    }

    public Task DatNguoiChinhAsync(int maHocVien, int maNguoiGiamHo, CancellationToken maHuy = default)
    {
        KiemTraQuyenQuanLy();
        return khoDuLieu.DatNguoiChinhAsync(maHocVien, maNguoiGiamHo, nguoiDung.UserId, maHuy);
    }

    public Task GoLienKetAsync(int maHocVien, int maNguoiGiamHo, CancellationToken maHuy = default)
    {
        KiemTraQuyenQuanLy();
        return khoDuLieu.GoLienKetAsync(maHocVien, maNguoiGiamHo, nguoiDung.UserId, maHuy);
    }

    private static PhanHoiNguoiGiamHo TaoPhanHoi(Guardian nguoiGiamHo) =>
        new(nguoiGiamHo.Id, nguoiGiamHo.FullName, nguoiGiamHo.Phone, nguoiGiamHo.Email, nguoiGiamHo.IsActive);
}
