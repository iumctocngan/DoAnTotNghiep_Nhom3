using CmsEdu.Application.Attendances;

namespace CmsEdu.Application.Common.Interfaces;

/// <summary>
/// Giao diện dịch vụ quản lý nghiệp vụ điểm danh (Attendance) cho buổi học.
/// </summary>
public interface IDichVuDiemDanh
{
    /// <summary>
    /// Lấy danh sách điểm danh của các học viên hợp lệ trong một buổi học.
    /// </summary>
    Task<PhanHoiDiemDanhBuoiHoc> LayDanhSachDiemDanhTheoBuoiHocAsync(
        int maBuoiHoc,
        CancellationToken maHuy = default);

    /// <summary>
    /// Lưu thông tin điểm danh cả lớp theo lô cho toàn bộ học viên hợp lệ trong một buổi học.
    /// </summary>
    Task<PhanHoiDiemDanhBuoiHoc> LuuDiemDanhTheoBuoiHocAsync(
        int maBuoiHoc,
        YeuCauLuuDiemDanhBuoiHoc yeuCau,
        CancellationToken maHuy = default);
}
