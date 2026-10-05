using CmsEdu.Application.Dashboard;

namespace CmsEdu.Application.Common.Interfaces;

public interface IRoleDashboardService
{
    Task<AdminDashboardResponse> GetAdminDashboardAsync(
        CancellationToken cancellationToken = default);

    Task<TeacherDashboardResponse> GetTeacherDashboardAsync(
        CancellationToken cancellationToken = default);

    Task<CustomerCareDashboardResponse> GetCustomerCareDashboardAsync(
        CancellationToken cancellationToken = default);

    Task<AccountingDashboardResponse> GetAccountingDashboardAsync(
        CancellationToken cancellationToken = default);
}
