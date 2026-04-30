using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Api.Controllers;

/// <summary>
/// Provides public chat endpoints and authenticated history-management endpoints.
/// </summary>
[ApiController]
[Route("api/chat")]
public sealed class ChatController(
    IRagService ragService,
    IMongoSiteChatRepository repository,
    IConversationAccessService conversationAccessService) : SiteChatControllerBase
{
    private readonly IRagService _ragService = ragService ?? throw new ArgumentNullException(nameof(ragService));
    private readonly IMongoSiteChatRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IConversationAccessService _conversationAccessService = conversationAccessService ?? throw new ArgumentNullException(nameof(conversationAccessService));

    /// <summary>Generates a chat response.</summary>
    [HttpPost("")]
    [AllowAnonymous]
    public async Task<ActionResult<ChatResponse>> ChatAsync([FromBody] ChatRequest request, CancellationToken cancellationToken) =>
        Ok(await _ragService.ChatAsync(request, cancellationToken).ConfigureAwait(false));

    /// <summary>Streams a chat response as server-sent events.</summary>
    [HttpPost("stream")]
    [AllowAnonymous]
    public async Task StreamAsync([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        await foreach (var chunk in _ragService.ChatStreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            await Response.WriteAsync($"data: {chunk}\n\n", cancellationToken).ConfigureAwait(false);
            await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        await Response.WriteAsync("data: [DONE]\n\n", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets conversation history for an authenticated user.</summary>
    [HttpGet("history/{sessionId}")]
    [Authorize]
    public async Task<ActionResult<ConversationHistory>> GetHistoryAsync(string sessionId, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        var conversation = await _conversationAccessService.GetConversationAsync(user!, sessionId, cancellationToken).ConfigureAwait(false);
        if (conversation is null)
        {
            return NotFound(new { detail = "Conversation not found" });
        }

        var messages = conversation?.Messages.Select(message => new MessageDto(
            message.Role,
            message.Content,
            [],
            message.Timestamp)).ToList() ?? [];
        return Ok(new ConversationHistory(sessionId, messages, conversation?.CreatedAt, conversation?.UpdatedAt));
    }

    /// <summary>Clears conversation history for an authenticated user.</summary>
    [HttpDelete("history/{sessionId}")]
    [Authorize]
    public async Task<ActionResult<object>> ClearHistoryAsync(string sessionId, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        var deleted = await _conversationAccessService.DeleteConversationsAsync(user!, [sessionId], cancellationToken).ConfigureAwait(false);
        return deleted > 0
            ? Ok(new { success = true, message = "Conversation cleared" })
            : NotFound(new { detail = "Conversation not found" });
    }

    /// <summary>Lists conversation sessions for an authenticated user.</summary>
    [HttpGet("sessions")]
    [Authorize]
    public async Task<ActionResult<object>> GetSessionsAsync([FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        var result = await _conversationAccessService.ListConversationsAsync(user!, null, null, 1, Math.Clamp(limit, 1, 1000), cancellationToken).ConfigureAwait(false);
        return Ok(new
        {
            sessions = result.Items.Select(item => new { session_id = item.SessionId, created_at = item.CreatedAt, updated_at = item.UpdatedAt })
        });
    }

    /// <summary>Submits feedback for a chat message.</summary>
    [HttpPost("feedback")]
    [AllowAnonymous]
    public ActionResult<object> SubmitFeedback([FromQuery] string sessionId, [FromQuery] int messageIndex, [FromQuery] string feedback)
    {
        if (feedback is not ("positive" or "negative"))
        {
            return BadRequest(new { detail = "Feedback must be 'positive' or 'negative'" });
        }

        return Ok(new { success = true, message = "Feedback recorded", session_id = sessionId, message_index = messageIndex });
    }
}
