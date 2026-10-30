using CmsEdu.Api.Authorization;
using CmsEdu.Application.Knowledge;
using CmsEdu.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CmsEdu.Api.Controllers;

[ApiController]
[Route("api/classes/{classId:int}/knowledge")]
[Authorize]
public class ClassChatController(IClassChatService chatService) : ControllerBase
{
    [HttpGet("info")]
    [HasPermission(Permissions.ClassAssistant.View)]
    public async Task<ActionResult<ClassKnowledgeInfoDto>> GetClassKnowledgeInfo(
        int classId,
        CancellationToken cancellationToken)
    {
        return Ok(await chatService.GetClassKnowledgeInfoAsync(classId, cancellationToken));
    }

    [HttpGet("messages")]
    [HasPermission(Permissions.ClassAssistant.View)]
    public async Task<ActionResult<List<ClassMessageDto>>> GetMessages(
        int classId,
        CancellationToken cancellationToken)
    {
        return Ok(await chatService.GetMessagesAsync(classId, cancellationToken));
    }

    [HttpPost("chat")]
    [HasPermission(Permissions.ClassAssistant.Chat)]
    public async Task<ActionResult<ClassChatResultDto>> Chat(
        int classId,
        [FromBody] ClassChatRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await chatService.ChatAsync(classId, request, cancellationToken));
    }

    [HttpDelete("messages")]
    [HasPermission(Permissions.ClassAssistant.Chat)]
    public async Task<IActionResult> ClearMessages(
        int classId,
        CancellationToken cancellationToken)
    {
        await chatService.ClearMessagesAsync(classId, cancellationToken);
        return NoContent();
    }
}
