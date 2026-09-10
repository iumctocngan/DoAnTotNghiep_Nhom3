using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Sessions;

namespace CmsEdu.Application.Common.Interfaces;

public interface ISessionService
{
    Task<PagedResult<SessionResponse>> GetSessionsAsync(
        int? classId,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SessionResponse> GetSessionByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<SessionResponse> CreateSessionAsync(
        CreateSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<SessionResponse> UpdateSessionAsync(
        int id,
        UpdateSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<SessionResponse> CancelSessionAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<SessionResponse> CompleteSessionAsync(
        int id,
        CancellationToken cancellationToken = default);
}
