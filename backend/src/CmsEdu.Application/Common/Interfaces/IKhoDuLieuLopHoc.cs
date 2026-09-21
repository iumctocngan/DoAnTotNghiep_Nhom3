using CmsEdu.Domain.Entities;

namespace CmsEdu.Application.Common.Interfaces;

public interface IKhoDuLieuLopHoc
{
    Task<List<Class>> LayLopAsync(CancellationToken maHuy, string? giaoVienId = null);
    Task<Class?> TimLopAsync(int id, CancellationToken maHuy);
    Task<int> DemSiSoAsync(int id, CancellationToken maHuy);
    Task<bool> TrungMaAsync(string ma, int? boQuaId, CancellationToken maHuy);
    Task<bool> CapDoHoatDongAsync(int id, CancellationToken maHuy);
    Task<bool> GiaoVienHopLeAsync(string id, CancellationToken maHuy);
    Task<bool> TrungLichAsync(string giaoVienId, int? boQuaId, DayOfWeek thu,
        TimeOnly batDau, TimeOnly ketThuc, DateOnly ngayBatDau, DateOnly? ngayKetThuc,
        CancellationToken maHuy);
    Task<bool> CoDuLieuLienQuanAsync(int id, CancellationToken maHuy);
    Task LuuAsync(CancellationToken maHuy);
    void Them(Class lop);
    void Xoa(Class lop);
    Task<T> TrongGiaoDichAsync<T>(Func<Task<T>> congViec, CancellationToken maHuy);
}
