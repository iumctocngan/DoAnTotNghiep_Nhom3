using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using CmsEdu.Api.Services;
using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Entities;
using CmsEdu.Domain.Enums;
using CmsEdu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{UserRole.Admin},{UserRole.CustomerCare}")]
[Route("api/ai/cskh-chat")]
public sealed class CskhChatController(
    AppDbContext db,
    ICurrentUser currentUser,
    IModule3AiClient aiClient,
    ILogger<CskhChatController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet("/api/ai/cskh/student-context/{studentId:int}")]
    public async Task<IActionResult> GetStudentContext(int studentId, CancellationToken cancellationToken)
    {
        if (!await db.Students.AsNoTracking().AnyAsync(student => student.Id == studentId, cancellationToken))
            return NotFound(new ProblemDetails { Title = "Không tìm thấy học viên." });

        try
        {
            return Ok(await aiClient.GetStudentContextAsync(studentId, cancellationToken));
        }
        catch (Module3AiException exception)
        {
            return MapAiServiceError(exception);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Cannot reach Module 3 AI Service.");
            return AiServiceUnavailable();
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Module 3 AI Service request timed out.");
            return AiServiceUnavailable();
        }
    }

    [HttpGet("/api/ai/dashboard/retention-queue")]
    public async Task<ActionResult<IReadOnlyList<AtRiskStudentResponse>>> GetRetentionQueue(
        CancellationToken cancellationToken)
    {
        var latestSnapshots = db.StudentRenewalPredictions
            .AsNoTracking()
            .Where(prediction => !db.StudentRenewalPredictions.Any(later =>
                later.EnrollmentId == prediction.EnrollmentId &&
                (later.CreatedAt > prediction.CreatedAt ||
                 (later.CreatedAt == prediction.CreatedAt && later.Id > prediction.Id))))
            .Where(prediction =>
                prediction.RiskLevel == RenewalRiskLevel.HighRisk &&
                prediction.Enrollment.Status == EnrollmentStatus.Active &&
                !prediction.Student.IsArchived)
            .OrderBy(prediction => prediction.DaysUntilPeriodEnd)
            .ThenBy(prediction => prediction.Student.FullName)
            .Select(prediction => new
            {
                prediction.StudentId,
                prediction.EnrollmentId,
                prediction.Student.StudentCode,
                StudentName = prediction.Student.FullName,
                ClassName = prediction.Enrollment.Class.Name,
                prediction.RenewalProbability,
                prediction.DaysUntilPeriodEnd,
                prediction.PredictionDate,
                prediction.ModelVersion,
                prediction.TopDriversJson
            })
            .Take(100);

        var snapshots = await latestSnapshots.ToListAsync(cancellationToken);
        var response = new List<AtRiskStudentResponse>(snapshots.Count);
        foreach (var snapshot in snapshots)
        {
            IReadOnlyList<ShapDriverResponse> drivers;
            try
            {
                drivers = JsonSerializer.Deserialize<List<ShapDriverResponse>>(
                              snapshot.TopDriversJson,
                              JsonOptions)
                          ?? [];
            }
            catch (JsonException exception)
            {
                logger.LogWarning(
                    exception,
                    "Invalid SHAP JSON in prediction for student {StudentId}.",
                    snapshot.StudentId);
                drivers = [];
            }

            response.Add(new AtRiskStudentResponse(
                snapshot.StudentId,
                snapshot.EnrollmentId,
                snapshot.RenewalProbability,
                nameof(RenewalRiskLevel.HighRisk),
                snapshot.DaysUntilPeriodEnd,
                drivers,
                snapshot.ModelVersion,
                snapshot.PredictionDate,
                snapshot.StudentCode,
                snapshot.StudentName,
                snapshot.ClassName));
        }

        return Ok(response);
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<CskhChatSessionResponse>> CreateSession(
        CreateCskhChatSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
            return Unauthorized(new ProblemDetails { Title = "Không xác định được tài khoản hiện tại." });

        var student = await db.Students
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.StudentId, cancellationToken);
        if (student is null)
            return NotFound(new ProblemDetails { Title = "Không tìm thấy học viên." });

        var now = DateTime.UtcNow;
        var session = new CskhChatSession
        {
            Id = Guid.NewGuid().ToString(),
            UserId = currentUser.UserId,
            StudentId = student.Id,
            Title = $"Tư vấn: {student.FullName}",
            StartedAt = now,
            LastActiveAt = now
        };
        db.CskhChatSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetHistory),
            new { sessionId = session.Id },
            new CskhChatSessionResponse(session.Id, session.StudentId, session.Title, session.StartedAt));
    }

    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<ActionResult<CskhChatMessageResponse>> SendMessage(
        string sessionId,
        SendCskhChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
            return Unauthorized(new ProblemDetails { Title = "Không xác định được tài khoản hiện tại." });
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new ProblemDetails { Title = "Câu hỏi không được để trống." });

        var session = await db.CskhChatSessions
            .SingleOrDefaultAsync(
                item => item.Id == sessionId && item.UserId == currentUser.UserId,
                cancellationToken);
        if (session is null)
            return NotFound(new ProblemDetails { Title = "Không tìm thấy phiên chat." });

        try
        {
            var context = await aiClient.GetStudentContextAsync(session.StudentId, cancellationToken);
            var answer = await aiClient.GenerateCallScriptAsync(
                session.StudentId,
                request.Content.Trim(),
                cancellationToken);

            var now = DateTime.UtcNow;
            var userMessage = new CskhChatMessage
            {
                SessionId = session.Id,
                Role = "User",
                Content = request.Content.Trim(),
                SentAt = now
            };
            var assistantMessage = new CskhChatMessage
            {
                SessionId = session.Id,
                Role = "Assistant",
                Content = FormatAssistantContent(answer),
                ContextDataJson = context.GetRawText(),
                SuggestedScriptJson = JsonSerializer.Serialize(answer.SuggestedCallScript, JsonOptions),
                SentAt = now
            };
            session.LastActiveAt = now;
            db.CskhChatMessages.AddRange(userMessage, assistantMessage);
            await db.SaveChangesAsync(cancellationToken);

            return Ok(ToResponse(assistantMessage, answer.SuggestedCallScript));
        }
        catch (Module3AiException exception)
        {
            return MapAiServiceError(exception);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Cannot reach Module 3 AI Service.");
            return AiServiceUnavailable();
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Module 3 AI Service request timed out.");
            return AiServiceUnavailable();
        }
    }

    [HttpGet("sessions/{sessionId}/history")]
    public async Task<ActionResult<IReadOnlyList<CskhChatMessageResponse>>> GetHistory(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
            return Unauthorized(new ProblemDetails { Title = "Không xác định được tài khoản hiện tại." });

        var ownsSession = await db.CskhChatSessions
            .AsNoTracking()
            .AnyAsync(
                session => session.Id == sessionId && session.UserId == currentUser.UserId,
                cancellationToken);
        if (!ownsSession)
            return NotFound(new ProblemDetails { Title = "Không tìm thấy phiên chat." });

        var messages = await db.CskhChatMessages
            .AsNoTracking()
            .Where(message => message.SessionId == sessionId)
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.Id)
            .ToListAsync(cancellationToken);

        var response = messages.Select(message =>
        {
            IReadOnlyList<string>? script = null;
            if (!string.IsNullOrWhiteSpace(message.SuggestedScriptJson))
            {
                try
                {
                    script = JsonSerializer.Deserialize<string[]>(message.SuggestedScriptJson, JsonOptions);
                }
                catch (JsonException exception)
                {
                    logger.LogWarning(
                        exception,
                        "Invalid stored call script for chat message {MessageId}.",
                        message.Id);
                }
            }

            return ToResponse(message, script);
        }).ToList();

        return Ok(response);
    }

    [HttpPost("/api/cskh/interactions")]
    public async Task<IActionResult> SaveInteraction(
        CreateCskhInteractionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
            return Unauthorized(new ProblemDetails { Title = "Không xác định được tài khoản hiện tại." });
        if (!Enum.IsDefined(request.Channel) || !Enum.IsDefined(request.GuardianFeedback))
            return BadRequest(new ProblemDetails { Title = "Kênh liên hệ hoặc phản hồi không hợp lệ." });
        if (string.IsNullOrWhiteSpace(request.Notes))
            return BadRequest(new ProblemDetails { Title = "Ghi chú không được để trống." });

        var studentExists = await db.Students
            .AsNoTracking()
            .AnyAsync(student => student.Id == request.StudentId, cancellationToken);
        if (!studentExists)
            return NotFound(new ProblemDetails { Title = "Không tìm thấy học viên." });

        var guardianLinked = await db.StudentGuardians
            .AsNoTracking()
            .AnyAsync(
                link => link.StudentId == request.StudentId && link.GuardianId == request.GuardianId,
                cancellationToken);
        if (!guardianLinked)
            return BadRequest(new ProblemDetails
            {
                Title = "Người giám hộ không được liên kết với học viên đã chọn."
            });

        db.CskhInteractions.Add(new CskhInteraction
        {
            StudentId = request.StudentId,
            GuardianId = request.GuardianId,
            StaffUserId = currentUser.UserId,
            Channel = request.Channel,
            GuardianFeedback = request.GuardianFeedback,
            Notes = request.Notes.Trim(),
            FollowUpDate = request.FollowUpDate,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private ObjectResult MapAiServiceError(Module3AiException exception)
    {
        var status = exception.StatusCode switch
        {
            StatusCodes.Status400BadRequest => StatusCodes.Status400BadRequest,
            StatusCodes.Status404NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status503ServiceUnavailable
        };
        var title = status == StatusCodes.Status503ServiceUnavailable
            ? "Dịch vụ AI tạm thời không khả dụng."
            : "AI Service không xử lý được yêu cầu.";
        return Problem(statusCode: status, title: title, detail: exception.Message);
    }

    private ObjectResult AiServiceUnavailable() =>
        Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Dịch vụ AI tạm thời không khả dụng.",
            detail: "Kiểm tra AI Service Module 3 và cấu hình kết nối rồi thử lại.");

    private static string FormatAssistantContent(AiCallScriptResponse answer) =>
        $"Tóm tắt\n{answer.Summary}\n\nPhân tích rủi ro\n{answer.RiskAnalysis}\n\nKế hoạch hành động\n{answer.ActionPlan}\n\nKịch bản tư vấn\n{string.Join("\n", answer.SuggestedCallScript.Select((step, index) => $"{index + 1}. {step}"))}";

    private static CskhChatMessageResponse ToResponse(
        CskhChatMessage message,
        IReadOnlyList<string>? script) =>
        new(message.Id, message.Role, message.Content, script, message.SentAt);
}

public sealed class CreateCskhChatSessionRequest
{
    [Range(1, int.MaxValue)]
    public int StudentId { get; init; }
}

public sealed class SendCskhChatMessageRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;
}

public sealed class CreateCskhInteractionRequest
{
    [Range(1, int.MaxValue)]
    public int StudentId { get; init; }

    [Range(1, int.MaxValue)]
    public int GuardianId { get; init; }

    public CskhChannel Channel { get; init; }
    public GuardianFeedback GuardianFeedback { get; init; }

    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Notes { get; init; } = string.Empty;

    public DateOnly? FollowUpDate { get; init; }
}

public sealed record CskhChatSessionResponse(
    string Id,
    int StudentId,
    string Title,
    DateTime StartedAt);

public sealed record CskhChatMessageResponse(
    int Id,
    string Role,
    string Content,
    IReadOnlyList<string>? SuggestedCallScript,
    DateTime SentAt);

public sealed record AtRiskStudentResponse(
    int StudentId,
    int EnrollmentId,
    double RenewalProbability,
    string RiskLevel,
    int DaysUntilPeriodEnd,
    IReadOnlyList<ShapDriverResponse> TopDrivers,
    string ModelVersion,
    DateOnly PredictionDate,
    string StudentCode,
    string StudentName,
    string? ClassName);

public sealed record ShapDriverResponse(
    [property: JsonPropertyName("feature")] string Feature,
    [property: JsonPropertyName("direction")] string Direction,
    [property: JsonPropertyName("impact")] double Impact,
    [property: JsonPropertyName("description")] string Description);
