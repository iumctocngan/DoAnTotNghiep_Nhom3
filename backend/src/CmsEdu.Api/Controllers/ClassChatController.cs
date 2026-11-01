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

    [HttpGet("conversations")]
    [HasPermission(Permissions.ClassAssistant.View)]
    public async Task<ActionResult<List<ClassConversationDto>>> GetConversations(
        int classId,
        CancellationToken cancellationToken)
    {
        return Ok(await chatService.GetConversationsAsync(classId, cancellationToken));
    }

    [HttpPost("conversations")]
    [HasPermission(Permissions.ClassAssistant.Chat)]
    public async Task<ActionResult<ClassConversationDto>> CreateConversation(
        int classId,
        [FromBody] CreateConversationRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await chatService.CreateConversationAsync(classId, request, cancellationToken));
    }

    [HttpPut("conversations/{conversationId:guid}")]
    [HasPermission(Permissions.ClassAssistant.Chat)]
    public async Task<ActionResult<ClassConversationDto>> UpdateConversationTitle(
        int classId,
        Guid conversationId,
        [FromBody] UpdateConversationTitleRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await chatService.UpdateConversationTitleAsync(classId, conversationId, request, cancellationToken));
    }

    [HttpDelete("conversations/{conversationId:guid}")]
    [HasPermission(Permissions.ClassAssistant.Chat)]
    public async Task<IActionResult> DeleteConversation(
        int classId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        await chatService.DeleteConversationAsync(classId, conversationId, cancellationToken);
        return NoContent();
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    [HasPermission(Permissions.ClassAssistant.View)]
    public async Task<ActionResult<List<ClassMessageDto>>> GetConversationMessages(
        int classId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        return Ok(await chatService.GetConversationMessagesAsync(classId, conversationId, cancellationToken));
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
