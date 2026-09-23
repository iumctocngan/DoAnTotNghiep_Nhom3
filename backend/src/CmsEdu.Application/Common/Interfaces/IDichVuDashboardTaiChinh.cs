using CmsEdu.Application.Dashboard;

namespace CmsEdu.Application.Common.Interfaces;

/// <summary>Giao dien tinh so lieu dashboard theo quyen nguoi dung.</summary>
public interface IDichVuDashboardTaiChinh
{
    /// <summary>
    /// Tinh doanh thu tu payment Confirmed theo PaidAt va cong no hien tai.
    /// </summary>
    Task<PhanHoiDashboardTaiChinh> LayDashboardKeToanAsync(
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken = default);
}
