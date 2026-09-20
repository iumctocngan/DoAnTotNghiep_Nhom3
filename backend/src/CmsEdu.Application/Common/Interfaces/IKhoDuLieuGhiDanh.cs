using CmsEdu.Domain.Entities;

namespace CmsEdu.Application.Common.Interfaces;

public interface IKhoDuLieuGhiDanh
{
    Task<List<Enrollment>> LayDanhSachAsync(int? lopId, int? hocVienId, CancellationToken maHuy);
    Task<Enrollment?> TimAsync(int id, CancellationToken maHuy);
    Task<Student?> TimHocVienAsync(int id, CancellationToken maHuy);
    Task<Class?> TimLopAsync(int id, CancellationToken maHuy);
    Task<bool> DaGhiDanhAsync(int hocVienId, CancellationToken maHuy);
    Task<int> DemSiSoAsync(int lopId, CancellationToken maHuy);
    void Them(Enrollment ghiDanh);
    Task LuuAsync(CancellationToken maHuy);
    Task<T> TrongGiaoDichAsync<T>(Func<Task<T>> congViec, CancellationToken maHuy);
}
