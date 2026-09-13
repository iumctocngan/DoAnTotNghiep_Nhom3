using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Sessions;

namespace CmsEdu.Application.Common.Interfaces;

/// <summary>
/// Giao diện dịch vụ quản lý các nghiệp vụ liên quan đến buổi học (Session).
/// </summary>
public interface IDichVuBuoiHoc
{
    Task<PagedResult<PhanHoiBuoiHoc>> LayDanhSachBuoiHocAsync(
        int? maLop,
        DateOnly? tuNgay,
        DateOnly? denNgay,
        int trang,
        int kichThuocTrang,
        CancellationToken maHuy = default);

    Task<PhanHoiBuoiHoc> LayChiTietBuoiHocTheoIdAsync(
        int maBuoiHoc,
        CancellationToken maHuy = default);

    Task<PhanHoiBuoiHoc> TaoBuoiHocAsync(
        YeuCauTaoBuoiHoc yeuCau,
        CancellationToken maHuy = default);

    Task<PhanHoiBuoiHoc> CapNhatBuoiHocAsync(
        int maBuoiHoc,
        YeuCauCapNhatBuoiHoc yeuCau,
        CancellationToken maHuy = default);

    Task<PhanHoiBuoiHoc> HuyBuoiHocAsync(
        int maBuoiHoc,
        CancellationToken maHuy = default);

    Task<PhanHoiBuoiHoc> HoanTatBuoiHocAsync(
        int maBuoiHoc,
        CancellationToken maHuy = default);
}
