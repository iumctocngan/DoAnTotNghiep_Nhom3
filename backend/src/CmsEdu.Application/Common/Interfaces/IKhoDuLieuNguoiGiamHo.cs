using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Entities;

namespace CmsEdu.Application.Common.Interfaces;

public interface IKhoDuLieuNguoiGiamHo
{
    Task<PagedResult<Guardian>> LayDanhSachAsync(string? tuKhoa, bool? dangHoatDong, int trang, int kichThuocTrang, CancellationToken maHuy);
    Task<Guardian?> TimTheoIdAsync(int maNguoiGiamHo, CancellationToken maHuy);
    Task LuuAsync(Guardian nguoiGiamHo, bool taoMoi, string? maNguoiDung, CancellationToken maHuy);
    Task XoaAsync(int maNguoiGiamHo, string? maNguoiDung, CancellationToken maHuy);
    Task<IReadOnlyList<StudentGuardian>> LayLienKetAsync(int maHocVien, string? maGiaoVien, CancellationToken maHuy);
    Task GanLienKetAsync(int maHocVien, int maNguoiGiamHo, string quanHe, bool laNguoiChinh, bool chiCapNhat, string? maNguoiDung, CancellationToken maHuy);
    Task DatNguoiChinhAsync(int maHocVien, int maNguoiGiamHo, string? maNguoiDung, CancellationToken maHuy);
    Task GoLienKetAsync(int maHocVien, int maNguoiGiamHo, string? maNguoiDung, CancellationToken maHuy);
}
