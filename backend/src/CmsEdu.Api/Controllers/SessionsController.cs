using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Application.Common.Models;
using CmsEdu.Application.Sessions;
using CmsEdu.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize]
public class SessionsController(ISessionService sessionService) : ControllerBase
{
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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SessionResponse>> GetSession(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await sessionService.GetSessionByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<SessionResponse>> CreateSession(
        CreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await sessionService.CreateSessionAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetSession), new { id = session.Id }, session);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<SessionResponse>> UpdateSession(
        int id,
        UpdateSessionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await sessionService.UpdateSessionAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<ActionResult<SessionResponse>> CancelSession(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await sessionService.CancelSessionAsync(id, cancellationToken));
    }

    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = $"{UserRole.Admin},{UserRole.Teacher}")]
    public async Task<ActionResult<SessionResponse>> CompleteSession(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await sessionService.CompleteSessionAsync(id, cancellationToken));
    }
}
