using CmsEdu.Application.Common.Models;
using CmsEdu.Domain.Entities;

namespace CmsEdu.Application.Common.Interfaces;

public interface IKhoDuLieuNhanXet
{
    Task<PagedResult<StudentRemark>> LayDanhSachAsync(int ghiDanhId, int trang, int kichThuocTrang,
        CancellationToken maHuy);
    Task<StudentRemark?> TimAsync(int id, CancellationToken maHuy);
    Task<Enrollment?> TimGhiDanhAsync(int id, CancellationToken maHuy);
    Task<Session?> TimBuoiHocAsync(int id, CancellationToken maHuy);
    void Them(StudentRemark nhanXet);
    Task LuuAsync(CancellationToken maHuy);
}
