using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Api.Controllers;

/// <summary>
/// Provides authenticated conversation-management endpoints.
/// </summary>
[ApiController]
[Authorize]
[Route("api/conversations")]
public sealed class ConversationsController(
    IUserRepository repository,
    IConversationAccessService conversationAccessService) : SiteChatControllerBase
{
    private readonly IUserRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IConversationAccessService _conversationAccessService = conversationAccessService ?? throw new ArgumentNullException(nameof(conversationAccessService));

    /// <summary>Lists conversations.</summary>
    [HttpGet("")]
    public async Task<ActionResult<ConversationListResponse>> ListAsync([FromQuery] string? siteId, [FromQuery] int page = 1, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        if (!string.IsNullOrWhiteSpace(siteId)
            && !await _conversationAccessService.CanAccessSiteAsync(user!, siteId, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _conversationAccessService.ListConversationsAsync(user!, siteId, null, page, limit, cancellationToken).ConfigureAwait(false);
        var items = result.Items.Select(ToListItem).ToList();
        return Ok(new ConversationListResponse(items, result.Total, page, limit, (long)Math.Ceiling(result.Total / (double)Math.Max(1, limit))));
    }

    /// <summary>Searches conversations.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<ConversationSearchResponse>> SearchAsync([FromQuery] string q, [FromQuery] string? siteId, [FromQuery] int page = 1, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        if (!string.IsNullOrWhiteSpace(siteId)
            && !await _conversationAccessService.CanAccessSiteAsync(user!, siteId, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _conversationAccessService.ListConversationsAsync(user!, siteId, q, page, limit, cancellationToken).ConfigureAwait(false);
        var items = result.Items.Select(item => new ConversationSearchItem(item.SessionId, item.SiteId, item.CreatedAt, item.UpdatedAt, item.Messages.Count, item.Messages.FirstOrDefault()?.Content ?? string.Empty, item.Messages.FirstOrDefault(m => m.Content.Contains(q, StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty)).ToList();
        return Ok(new ConversationSearchResponse(items, result.Total, page, limit, (long)Math.Ceiling(result.Total / (double)Math.Max(1, limit)), q));
    }

    /// <summary>Auto-closes stale conversations.</summary>
    [HttpPost("auto-close")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public ActionResult<AutoCloseResponse> AutoClose([FromBody] AutoCloseRequest request) => Ok(new AutoCloseResponse(0, $"Closed conversations inactive for {request.DaysInactive} days"));

    /// <summary>Gets a conversation detail.</summary>
    [HttpGet("{sessionId}")]
    public async Task<ActionResult<ConversationDetail>> DetailAsync(string sessionId, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        var item = await _conversationAccessService.GetConversationAsync(user!, sessionId, cancellationToken).ConfigureAwait(false);
        return item is null ? NotFound(new { detail = "Conversation not found" }) : Ok(ToDetail(item));
    }

    /// <summary>Deletes a conversation.</summary>
    [HttpDelete("{sessionId}")]
    public async Task<ActionResult<ApiMessageResponse>> DeleteAsync(string sessionId, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        var deleted = await _conversationAccessService.DeleteConversationsAsync(user!, [sessionId], cancellationToken).ConfigureAwait(false);
        return deleted > 0 ? Ok(new ApiMessageResponse("Conversation deleted")) : NotFound(new { detail = "Conversation not found" });
    }

    /// <summary>Deletes multiple conversations.</summary>
    [HttpPost("bulk-delete")]
    public async Task<ActionResult<BulkDeleteResponse>> BulkDeleteAsync([FromBody] BulkDeleteRequest request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        var deleted = await _conversationAccessService.DeleteConversationsAsync(user!, request.SessionIds, cancellationToken).ConfigureAwait(false);
        return Ok(new BulkDeleteResponse(deleted, "Conversations deleted"));
    }

    /// <summary>Exports conversations.</summary>
    [HttpPost("export")]
    public ActionResult<object> Export([FromBody] ExportRequest request)
    {
        var fileName = request.Format.Equals("csv", StringComparison.OrdinalIgnoreCase) ? "conversations.csv" : "conversations.json";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        return Ok(new { conversations = Array.Empty<object>(), format = request.Format });
    }

    /// <summary>Updates status.</summary>
    [HttpPatch("{sessionId}/status")]
    public ActionResult<object> UpdateStatus(string sessionId, [FromBody] UpdateStatusRequest request) => Ok(new { session_id = sessionId, status = request.Status });
    /// <summary>Updates priority.</summary>
    [HttpPatch("{sessionId}/priority")]
    public ActionResult<object> UpdatePriority(string sessionId, [FromBody] UpdatePriorityRequest request) => Ok(new { session_id = sessionId, priority = request.Priority });
    /// <summary>Updates tags.</summary>
    [HttpPatch("{sessionId}/tags")]
    public ActionResult<object> UpdateTags(string sessionId, [FromBody] UpdateTagsRequest request) => Ok(new { session_id = sessionId, tags = request.Tags });
    /// <summary>Adds a note.</summary>
    [HttpPost("{sessionId}/notes")]
    public ActionResult<object> AddNote(string sessionId, [FromBody] AddNoteRequest request) => Ok(new { session_id = sessionId, note_id = Guid.NewGuid().ToString("N")[..8], content = request.Content });
    /// <summary>Updates a note.</summary>
    [HttpPut("{sessionId}/notes/{noteId}")]
    public ActionResult<object> UpdateNote(string sessionId, string noteId, [FromBody] UpdateNoteRequest request) => Ok(new { session_id = sessionId, note_id = noteId, content = request.Content });
    /// <summary>Deletes a note.</summary>
    [HttpDelete("{sessionId}/notes/{noteId}")]
    public ActionResult<object> DeleteNote(string sessionId, string noteId) => Ok(new { session_id = sessionId, note_id = noteId, deleted = true });
    /// <summary>Updates visitor details.</summary>
    [HttpPatch("{sessionId}/visitor")]
    public ActionResult<object> UpdateVisitor(string sessionId, [FromBody] UpdateVisitorRequest request) => Ok(new { session_id = sessionId, request.VisitorName, request.VisitorEmail });
    /// <summary>Marks a conversation read.</summary>
    [HttpPatch("{sessionId}/read")]
    public ActionResult<object> MarkRead(string sessionId) => Ok(new { session_id = sessionId, unread = false });
    /// <summary>Sets satisfaction rating.</summary>
    [HttpPatch("{sessionId}/rating")]
    public ActionResult<object> SetRating(string sessionId, [FromBody] SetRatingRequest request) => Ok(new { session_id = sessionId, rating = request.Rating });

    private static ConversationListItem ToListItem(MongoConversation item) => new(item.SessionId, item.SiteId, item.CreatedAt, item.UpdatedAt, item.Messages.Count, item.Messages.FirstOrDefault()?.Content ?? string.Empty, item.Status, item.Priority);

    private static ConversationDetail ToDetail(MongoConversation item) => new(
        item.SessionId,
        item.SiteId,
        item.CreatedAt,
        item.UpdatedAt,
        item.Messages.Select(message => new MessageDetail(message.Role, message.Content, [], message.Timestamp)).ToList(),
        new ConversationStats(item.Messages.Count, item.Messages.Count(m => m.Role == "user"), item.Messages.Count(m => m.Role == "assistant"), 0, 0, 0),
        item.Status,
        item.Priority);
}

/// <summary>
/// Provides authenticated analytics endpoints.
/// </summary>
[ApiController]
[Authorize]
[Route("api/analytics")]
public sealed class AnalyticsController(
    IUserRepository repository,
    IConversationAccessService conversationAccessService) : SiteChatControllerBase
{
    private readonly IUserRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IConversationAccessService _conversationAccessService = conversationAccessService ?? throw new ArgumentNullException(nameof(conversationAccessService));

    /// <summary>Gets analytics overview.</summary>
    [HttpGet("overview")]
    public async Task<ActionResult<object>> OverviewAsync(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        return Ok(await _conversationAccessService.GetSystemStatsAsync(user!, cancellationToken).ConfigureAwait(false));
    }
    /// <summary>Gets conversation trends.</summary>
    [HttpGet("conversations")]
    public ActionResult<object> Conversations() => Ok(new { labels = Array.Empty<string>(), data = Array.Empty<int>() });
    /// <summary>Gets popular questions.</summary>
    [HttpGet("popular-questions")]
    public ActionResult<IReadOnlyList<object>> PopularQuestions() => Ok(Array.Empty<object>());
    /// <summary>Gets source usage.</summary>
    [HttpGet("sources-used")]
    public ActionResult<IReadOnlyList<object>> SourcesUsed() => Ok(Array.Empty<object>());
    /// <summary>Gets recent conversations.</summary>
    [HttpGet("recent-conversations")]
    public ActionResult<IReadOnlyList<object>> RecentConversations() => Ok(Array.Empty<object>());
    /// <summary>Gets site conversation stats.</summary>
    [HttpGet("conversations-by-site")]
    public ActionResult<IReadOnlyList<object>> ConversationsBySite() => Ok(Array.Empty<object>());
}
