using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Entities;
namespace CmsEdu.Application.Common.Interfaces;
public interface IKhoDuLieuHocVien
{
    Task<PagedResult<Student>> LayDanhSachAsync(string? tuKhoa, bool? daLuuTru, string? maGiaoVien, int trang, int kichThuocTrang, CancellationToken maHuy);
    Task<Student?> TimTheoIdAsync(int maDinhDanh, string? maGiaoVien, CancellationToken maHuy);
    Task<bool> MaDaTonTaiAsync(string maHocVien, int? maLoaiTru, CancellationToken maHuy);
    Task LuuAsync(Student hocVien, bool laTaoMoi, string? maNguoiDung, CancellationToken maHuy);
    Task LuuTruAsync(int maDinhDanh, string? maNguoiDung, CancellationToken maHuy);
    Task KhoiPhucAsync(int maDinhDanh, string? maNguoiDung, CancellationToken maHuy);
}
