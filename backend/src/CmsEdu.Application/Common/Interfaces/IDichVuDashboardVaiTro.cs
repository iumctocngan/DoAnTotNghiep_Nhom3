using CmsEdu.Application.Dashboard;

namespace CmsEdu.Application.Common.Interfaces;

public interface IDichVuDashboardVaiTro
{
    Task<PhanHoiDashboardAdmin> LayDashboardAdminAsync(
        CancellationToken cancellationToken = default);

    Task<PhanHoiDashboardTeacher> LayDashboardTeacherAsync(
        CancellationToken cancellationToken = default);

    Task<PhanHoiDashboardCustomerCare> LayDashboardCustomerCareAsync(
        CancellationToken cancellationToken = default);

    Task<PhanHoiDashboardTaiChinh> LayDashboardKeToanAsync(
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken = default);
}
