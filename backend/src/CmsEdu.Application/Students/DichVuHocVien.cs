using System.ComponentModel.DataAnnotations;
using CmsEdu.Application.Common.Exceptions;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
namespace CmsEdu.Application.Students;

public class DichVuHocVien(IKhoDuLieuHocVien khoDuLieu, ICurrentUser nguoiDungHienTai) : IDichVuHocVien
{
    private string? LayPhamViTruyCap()
    {
        if (!nguoiDungHienTai.IsAuthenticated) throw new UnauthorizedAccessException();
        return nguoiDungHienTai.Role switch
        {
            UserRole.Admin or UserRole.CustomerCare or UserRole.Accountant => null,
            UserRole.Teacher when !string.IsNullOrWhiteSpace(nguoiDungHienTai.UserId) => nguoiDungHienTai.UserId,
            _ => throw new ForbiddenAccessException("Không có quyền xem hồ sơ học viên.")
        };
    }

    private void KiemTraQuyenGhi(bool luuTru = false)
    {
        if (!nguoiDungHienTai.IsAuthenticated) throw new UnauthorizedAccessException();
        if (nguoiDungHienTai.Role != UserRole.Admin && (luuTru || nguoiDungHienTai.Role != UserRole.CustomerCare))
            throw new ForbiddenAccessException("Không có quyền thay đổi hồ sơ học viên.");
    }

    public async Task<PagedResult<PhanHoiHocVien>> LayDanhSachHocVienAsync(string? tuKhoa, bool? daLuuTru, int trang, int kichThuocTrang, CancellationToken maHuy = default)
    {
        var phamVi = LayPhamViTruyCap();
        if (trang < 1 || kichThuocTrang < 1 || kichThuocTrang > 100 || (long)(trang - 1) * kichThuocTrang > int.MaxValue)
            throw new ValidationException("Số trang phải từ 1 trở lên, kích thước trang từ 1 đến 100 và không vượt giới hạn phân trang.");
        var ketQua = await khoDuLieu.LayDanhSachAsync(tuKhoa?.Trim(), daLuuTru, phamVi, trang, kichThuocTrang, maHuy);
        return new(ketQua.Items.Select(TaoPhanHoi).ToList(), ketQua.Page, ketQua.PageSize, ketQua.TotalItems);
    }

    public async Task<PhanHoiHocVien> LayHocVienTheoIdAsync(int maDinhDanh, CancellationToken maHuy = default)
    {
        var hocVien = await khoDuLieu.TimTheoIdAsync(maDinhDanh, LayPhamViTruyCap(), maHuy)
            ?? throw new NotFoundException("Không tìm thấy học viên trong phạm vi truy cập.");
        return TaoPhanHoi(hocVien);
    }

    public async Task<PhanHoiHocVien> TaoHocVienAsync(YeuCauTaoHocVien yeuCau, CancellationToken maHuy = default)
    {
        KiemTraQuyenGhi();
        KiemTraDuLieu(yeuCau);
        var hocVien = new Student();
        await GanDuLieuAsync(hocVien, yeuCau, maHuy);
        await khoDuLieu.LuuAsync(hocVien, true, nguoiDungHienTai.UserId, maHuy);
        return TaoPhanHoi(hocVien);
    }

    public async Task<PhanHoiHocVien> CapNhatHocVienAsync(int maDinhDanh, YeuCauCapNhatHocVien yeuCau, CancellationToken maHuy = default)
    {
        KiemTraQuyenGhi();
        KiemTraDuLieu(yeuCau);
        var hocVien = await khoDuLieu.TimTheoIdAsync(maDinhDanh, null, maHuy) ?? throw new NotFoundException("Không tìm thấy học viên.");
        if (hocVien.IsArchived)
            throw new ConflictException("Cần khôi phục hồ sơ học viên trước khi cập nhật.");
        await GanDuLieuAsync(hocVien, yeuCau, maHuy);
        await khoDuLieu.LuuAsync(hocVien, false, nguoiDungHienTai.UserId, maHuy);
        return TaoPhanHoi(hocVien);
    }

    public Task LuuTruHocVienAsync(int maDinhDanh, CancellationToken maHuy = default)
    {
        KiemTraQuyenGhi(true);
        return khoDuLieu.LuuTruAsync(maDinhDanh, nguoiDungHienTai.UserId, maHuy);
    }

    private static void KiemTraDuLieu(YeuCauHocVien yeuCau) => Validator.ValidateObject(yeuCau, new ValidationContext(yeuCau), true);

    public Task KhoiPhucHocVienAsync(int maDinhDanh, CancellationToken maHuy = default)
    {
        KiemTraQuyenGhi(true);
        return khoDuLieu.KhoiPhucAsync(maDinhDanh, nguoiDungHienTai.UserId, maHuy);
    }

    private async Task GanDuLieuAsync(Student hocVien, YeuCauHocVien yeuCau, CancellationToken maHuy)
    {
        var maHocVien = yeuCau.StudentCode.Trim();
        if (await khoDuLieu.MaDaTonTaiAsync(maHocVien, hocVien.Id == 0 ? null : hocVien.Id, maHuy))
            throw new ConflictException("Mã học viên đã tồn tại.");
        hocVien.StudentCode = maHocVien;
        hocVien.FullName = yeuCau.FullName.Trim();
        hocVien.DateOfBirth = yeuCau.DateOfBirth;
        hocVien.Gender = yeuCau.Gender;
        hocVien.LearningNote = string.IsNullOrWhiteSpace(yeuCau.LearningNote) ? null : yeuCau.LearningNote.Trim();
    }

    private PhanHoiHocVien TaoPhanHoi(Student hocVien) => new(hocVien.Id, hocVien.StudentCode, hocVien.FullName,
        hocVien.DateOfBirth, hocVien.Gender, nguoiDungHienTai.Role == UserRole.Accountant ? null : hocVien.LearningNote, hocVien.IsArchived);
}
