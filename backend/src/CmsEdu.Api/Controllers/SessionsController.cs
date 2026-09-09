using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Sessions;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

/// <summary>
/// Cung cấp API quản lý buổi học: xem, tạo, cập nhật, hủy và hoàn tất Session.
/// </summary>
[ApiController]
[Route("api/sessions")]
[Authorize]
public class SessionsController(ISessionService sessionService) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách Session theo phạm vi quyền, có thể lọc theo lớp và khoảng ngày.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<SessionResponse>>> GetSessions(
        [FromQuery] int? classId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await sessionService.GetSessionsAsync(
            classId, fromDate, toDate, page, pageSize, cancellationToken));
    }

    /// <summary>
    /// Lấy thông tin chi tiết của một Session theo phạm vi quyền của người dùng hiện tại.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SessionResponse>> GetSession(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await sessionService.GetSessionByIdAsync(id, cancellationToken));
    }

    /// <summary>
    /// Tạo một Session mới cho lớp đang hoạt động. Chỉ Admin được thực hiện.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<SessionResponse>> CreateSession(
        CreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await sessionService.CreateSessionAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetSession), new { id = session.Id }, session);
    }

    /// <summary>
    /// Cập nhật Session đang ở trạng thái Scheduled. Chỉ Admin được thực hiện.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<SessionResponse>> UpdateSession(
        int id,
        UpdateSessionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await sessionService.UpdateSessionAsync(id, request, cancellationToken));
    }

    /// <summary>
    /// Hủy Session đang ở trạng thái Scheduled. Chỉ Admin được thực hiện.
    /// </summary>
    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<SessionResponse>> CancelSession(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await sessionService.CancelSessionAsync(id, cancellationToken));
    }

    /// <summary>
    /// Hoàn tất Session đang ở trạng thái Scheduled khi đã điểm danh đủ học viên hợp lệ.
    /// </summary>
    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.Teacher}")]
    public async Task<ActionResult<SessionResponse>> CompleteSession(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await sessionService.CompleteSessionAsync(id, cancellationToken));
    }
}
