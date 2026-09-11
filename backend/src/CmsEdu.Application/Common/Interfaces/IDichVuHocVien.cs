using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Students;
namespace CmsEdu.Application.Common.Interfaces;
public interface IDichVuHocVien
{
    Task<PagedResult<PhanHoiHocVien>> LayDanhSachHocVienAsync(string? tuKhoa, bool? daLuuTru, int trang, int kichThuocTrang, CancellationToken maHuy = default);
    Task<PhanHoiHocVien> LayHocVienTheoIdAsync(int maDinhDanh, CancellationToken maHuy = default);
    Task<PhanHoiHocVien> TaoHocVienAsync(YeuCauTaoHocVien yeuCau, CancellationToken maHuy = default);
    Task<PhanHoiHocVien> CapNhatHocVienAsync(int maDinhDanh, YeuCauCapNhatHocVien yeuCau, CancellationToken maHuy = default);
    Task LuuTruHocVienAsync(int maDinhDanh, CancellationToken maHuy = default);
    Task KhoiPhucHocVienAsync(int maDinhDanh, CancellationToken maHuy = default);
}
