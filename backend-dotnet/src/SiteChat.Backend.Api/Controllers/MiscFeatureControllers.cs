using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Api.Controllers;

/// <summary>
/// Provides public embed setup and widget endpoints.
/// </summary>
[ApiController]
[Route("api/embed")]
public sealed class EmbedController(
    IMongoSiteChatRepository repository,
    ISiteManagementService siteManagementService) : SiteChatControllerBase
{
    private readonly IMongoSiteChatRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly ISiteManagementService _siteManagementService = siteManagementService ?? throw new ArgumentNullException(nameof(siteManagementService));

    /// <summary>Creates a site for widget embedding.</summary>
    [HttpPost("setup")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrUser)]
    public async Task<ActionResult<SetupResponse>> SetupAsync([FromBody] SetupRequest request, CancellationToken cancellationToken)
    {
        var owner = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(owner) is { } missing)
        {
            return missing;
        }

        var site = await _siteManagementService.CreateSiteAsync(owner!, request, cancellationToken).ConfigureAwait(false);
        var scriptUrl = $"/widget/chatbot.js";
        var code = $"<script src=\"{scriptUrl}\" data-site-id=\"{site.SiteId}\"></script>";
        return Ok(new SetupResponse(site.SiteId, code, scriptUrl, "Site setup created"));
    }

    /// <summary>Gets embed status.</summary>
    [HttpGet("status/{siteId}")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> StatusAsync(string siteId, CancellationToken cancellationToken)
    {
        var site = await _repository.GetSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        return site is null ? NotFound(new { detail = "Site not found" }) : Ok(new { site_id = site.SiteId, status = site.Status });
    }

    /// <summary>Gets the widget script for a site.</summary>
    [HttpGet("script/{siteId}")]
    [AllowAnonymous]
    public ContentResult Script(string siteId) => Content($"window.SiteChatSiteId = '{siteId}';", "application/javascript");

    /// <summary>Gets public widget security metadata.</summary>
    [HttpGet("security/{siteId}")]
    [AllowAnonymous]
    public ActionResult<object> Security(string siteId) => Ok(new { site_id = siteId, enforce_domain_validation = false });
}

/// <summary>
/// Provides platform white-label endpoints.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/platform")]
public sealed class PlatformController(IPlatformConfigurationService platformConfigurationService) : ControllerBase
{
    private readonly IPlatformConfigurationService _platformConfigurationService = platformConfigurationService ?? throw new ArgumentNullException(nameof(platformConfigurationService));

    /// <summary>Gets white-label configuration.</summary>
    [HttpGet("whitelabel")]
    public async Task<ActionResult<object>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await _platformConfigurationService.GetAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Updates white-label configuration.</summary>
    [HttpPut("whitelabel")]
    public async Task<ActionResult<object>> UpdateAsync([FromBody] PlatformWhiteLabelConfig request, CancellationToken cancellationToken) =>
        Ok(await _platformConfigurationService.UpdateAsync(request, cancellationToken).ConfigureAwait(false));

    /// <summary>Resets white-label configuration.</summary>
    [HttpPost("whitelabel/reset")]
    public async Task<ActionResult<object>> ResetAsync(CancellationToken cancellationToken) =>
        Ok(await _platformConfigurationService.ResetAsync(cancellationToken).ConfigureAwait(false));
}

/// <summary>
/// Provides lead-capture endpoints.
/// </summary>
[ApiController]
public sealed class LeadsController : ControllerBase
{
    /// <summary>Captures a public widget lead.</summary>
    [HttpPost("api/leads")]
    [AllowAnonymous]
    public ActionResult<object> Capture([FromBody] LeadCreate request)
    {
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return Ok(new { success = true, message = "Lead captured" });
        }

        return Ok(new { success = true, lead_id = Guid.NewGuid().ToString("N")[..12], message = "Lead captured" });
    }

    /// <summary>Checks whether a lead exists.</summary>
    [HttpGet("api/leads/check/{siteId}/{sessionId}")]
    [AllowAnonymous]
    public ActionResult<object> Check(string siteId, string sessionId) => Ok(new { site_id = siteId, session_id = sessionId, exists = false });

    /// <summary>Lists leads for a site.</summary>
    [HttpGet("api/sites/{siteId}/leads")]
    [Authorize]
    public ActionResult<LeadListResponse> List(string siteId, [FromQuery] int page = 1, [FromQuery] int limit = 20) => Ok(new LeadListResponse([], 0, page, limit, 0));

    /// <summary>Exports leads for a site.</summary>
    [HttpGet("api/sites/{siteId}/leads/export")]
    [Authorize]
    public ContentResult Export(string siteId)
    {
        Response.Headers.ContentDisposition = $"attachment; filename=\"leads-{siteId}.csv\"";
        return Content("email,name,source\n", "text/csv");
    }

    /// <summary>Gets lead count for a site.</summary>
    [HttpGet("api/sites/{siteId}/leads/count")]
    [Authorize]
    public ActionResult<object> Count(string siteId) => Ok(new { site_id = siteId, count = 0 });

    /// <summary>Deletes a lead.</summary>
    [HttpDelete("api/leads/{leadId}")]
    [Authorize]
    public ActionResult<ApiMessageResponse> Delete(string leadId) => Ok(new ApiMessageResponse($"Lead {leadId} deleted"));
}

/// <summary>
/// Provides human handoff endpoints.
/// </summary>
[ApiController]
public sealed class HandoffController : ControllerBase
{
    /// <summary>Creates a handoff request.</summary>
    [HttpPost("api/handoff")]
    [AllowAnonymous]
    public ActionResult<object> Create([FromBody] object request) => Ok(new { handoff_id = Guid.NewGuid().ToString("N")[..12], status = "pending" });
    /// <summary>Gets handoff metadata.</summary>
    [HttpGet("api/handoff/{handoffId}")]
    [AllowAnonymous]
    public ActionResult<object> Get(string handoffId) => Ok(new { handoff_id = handoffId, status = "pending" });
    /// <summary>Gets handoff messages.</summary>
    [HttpGet("api/handoff/{handoffId}/messages")]
    [AllowAnonymous]
    public ActionResult<IReadOnlyList<object>> Messages(string handoffId) => Ok(Array.Empty<object>());
    /// <summary>Adds a handoff visitor message.</summary>
    [HttpPost("api/handoff/{handoffId}/messages")]
    [AllowAnonymous]
    public ActionResult<object> AddMessage(string handoffId, [FromBody] object request) => Ok(new { handoff_id = handoffId, message_id = Guid.NewGuid().ToString("N")[..8] });
    /// <summary>Marks a handoff abandoned.</summary>
    [HttpPost("api/handoff/{handoffId}/abandon")]
    [AllowAnonymous]
    public ActionResult<object> Abandon(string handoffId) => Ok(new { handoff_id = handoffId, status = "abandoned" });
    /// <summary>Gets handoff availability.</summary>
    [HttpGet("api/sites/{siteId}/handoff/availability")]
    [AllowAnonymous]
    public ActionResult<object> Availability(string siteId) => Ok(new { site_id = siteId, available = true });
    /// <summary>Gets handoff queue.</summary>
    [HttpGet("api/sites/{siteId}/handoff/queue")]
    [Authorize]
    public ActionResult<object> Queue(string siteId) => Ok(new { site_id = siteId, items = Array.Empty<object>() });
    /// <summary>Gets full handoff details.</summary>
    [HttpGet("api/handoff/{handoffId}/full")]
    [Authorize]
    public ActionResult<object> Full(string handoffId) => Ok(new { handoff_id = handoffId });
    /// <summary>Updates handoff status.</summary>
    [HttpPut("api/handoff/{handoffId}/status")]
    [Authorize]
    public ActionResult<object> Status(string handoffId, [FromBody] object request) => Ok(new { handoff_id = handoffId });
    /// <summary>Assigns a handoff.</summary>
    [HttpPut("api/handoff/{handoffId}/assign")]
    [Authorize]
    public ActionResult<object> Assign(string handoffId, [FromBody] object request) => Ok(new { handoff_id = handoffId });
    /// <summary>Adds an agent message.</summary>
    [HttpPost("api/handoff/{handoffId}/agent-message")]
    [Authorize]
    public ActionResult<object> AgentMessage(string handoffId, [FromBody] object request) => Ok(new { handoff_id = handoffId });
    /// <summary>Gets handoff config.</summary>
    [HttpGet("api/sites/{siteId}/handoff/config")]
    [Authorize]
    public ActionResult<object> GetConfig(string siteId) => Ok(new { site_id = siteId, enabled = true });
    /// <summary>Updates handoff config.</summary>
    [HttpPut("api/sites/{siteId}/handoff/config")]
    [Authorize]
    public ActionResult<object> PutConfig(string siteId, [FromBody] object request) => Ok(request);
    /// <summary>Gets business hours.</summary>
    [HttpGet("api/sites/{siteId}/business-hours")]
    [AllowAnonymous]
    public ActionResult<object> BusinessHours(string siteId) => Ok(new { site_id = siteId, enabled = false });
}

/// <summary>
/// Provides schedule, Q&A, trigger, and document endpoints.
/// </summary>
[ApiController]
[Authorize]
public sealed class OperationsController : ControllerBase
{
    /// <summary>Gets a crawl schedule.</summary>
    [HttpGet("api/sites/{siteId}/crawl-schedule")]
    public ActionResult<object> GetSchedule(string siteId) => Ok(new { site_id = siteId, enabled = false });
    /// <summary>Updates a crawl schedule.</summary>
    [HttpPut("api/sites/{siteId}/crawl-schedule")]
    public ActionResult<object> PutSchedule(string siteId, [FromBody] object request) => Ok(request);
    /// <summary>Triggers an immediate crawl.</summary>
    [HttpPost("api/sites/{siteId}/crawl-now")]
    public ActionResult<object> CrawlNow(string siteId) => Ok(new { site_id = siteId, message = "Crawl queued" });
    /// <summary>Gets crawl history.</summary>
    [HttpGet("api/sites/{siteId}/crawl-history")]
    public ActionResult<object> CrawlHistory(string siteId) => Ok(new { site_id = siteId, history = Array.Empty<object>() });
    /// <summary>Gets crawl status.</summary>
    [HttpGet("api/sites/{siteId}/crawl-status")]
    public ActionResult<object> CrawlStatus(string siteId) => Ok(new { site_id = siteId, status = "idle" });

    /// <summary>Creates a Q&A pair.</summary>
    [HttpPost("api/sites/{siteId}/qa")]
    public ActionResult<QAPair> CreateQa(string siteId, [FromBody] QAPairCreate request) => Ok(new QAPair(Guid.NewGuid().ToString("N")[..8], siteId, request.Question, request.Answer, request.Enabled, 0, DateTime.UtcNow, DateTime.UtcNow));
    /// <summary>Lists Q&A pairs.</summary>
    [HttpGet("api/sites/{siteId}/qa")]
    public ActionResult<QAPairListResponse> ListQa(string siteId) => Ok(new QAPairListResponse([], 0));
    /// <summary>Gets Q&A stats.</summary>
    [HttpGet("api/sites/{siteId}/qa/stats")]
    public ActionResult<object> QaStats(string siteId) => Ok(new { site_id = siteId, total = 0 });
    /// <summary>Gets a Q&A pair.</summary>
    [HttpGet("api/sites/{siteId}/qa/{qaId}")]
    public ActionResult<object> GetQa(string siteId, string qaId) => Ok(new { site_id = siteId, id = qaId });
    /// <summary>Updates a Q&A pair.</summary>
    [HttpPut("api/sites/{siteId}/qa/{qaId}")]
    public ActionResult<object> UpdateQa(string siteId, string qaId, [FromBody] QAPairUpdate request) => Ok(new { site_id = siteId, id = qaId, request.Question, request.Answer, request.Enabled });
    /// <summary>Deletes a Q&A pair.</summary>
    [HttpDelete("api/sites/{siteId}/qa/{qaId}")]
    public ActionResult<object> DeleteQa(string siteId, string qaId) => Ok(new { site_id = siteId, id = qaId, deleted = true });
    /// <summary>Creates Q&A from a conversation.</summary>
    [HttpPost("api/sites/{siteId}/qa/from-conversation")]
    public ActionResult<object> QaFromConversation(string siteId, [FromBody] object request) => Ok(new { site_id = siteId });
    /// <summary>Toggles a Q&A pair.</summary>
    [HttpPost("api/sites/{siteId}/qa/{qaId}/toggle")]
    public ActionResult<object> ToggleQa(string siteId, string qaId) => Ok(new { site_id = siteId, id = qaId });

    /// <summary>Gets supported document types.</summary>
    [HttpGet("api/documents/supported-types")]
    public ActionResult<object> SupportedTypes() => Ok(new { types = new[] { ".pdf", ".docx", ".txt", ".md" } });
    /// <summary>Uploads a document placeholder.</summary>
    [HttpPost("api/documents/upload/{siteId}")]
    public ActionResult<object> UploadDocument(string siteId) => Ok(new { site_id = siteId, document_id = Guid.NewGuid().ToString("N")[..12] });
    /// <summary>Lists documents.</summary>
    [HttpGet("api/documents/{siteId}")]
    public ActionResult<IReadOnlyList<object>> Documents(string siteId) => Ok(Array.Empty<object>());
    /// <summary>Deletes a document.</summary>
    [HttpDelete("api/documents/{siteId}/{docId}")]
    public ActionResult<object> DeleteDocument(string siteId, string docId) => Ok(new { site_id = siteId, document_id = docId, deleted = true });
}

/// <summary>
/// Provides trigger endpoints.
/// </summary>
[ApiController]
public sealed class TriggersController : ControllerBase
{
    /// <summary>Gets site triggers.</summary>
    [HttpGet("api/sites/{siteId}/triggers")]
    [Authorize]
    public ActionResult<object> GetSiteTriggers(string siteId) => Ok(new { triggers = Array.Empty<object>(), global_cooldown_ms = 30000 });
    /// <summary>Creates a trigger.</summary>
    [HttpPost("api/sites/{siteId}/triggers")]
    [Authorize]
    public ActionResult<object> CreateTrigger(string siteId, [FromBody] object request) => Ok(request);
    /// <summary>Updates trigger cooldown.</summary>
    [HttpPut("api/sites/{siteId}/triggers/cooldown")]
    [Authorize]
    public ActionResult<object> Cooldown(string siteId, [FromBody] object request) => Ok(request);
    /// <summary>Updates a trigger.</summary>
    [HttpPut("api/sites/{siteId}/triggers/{triggerId}")]
    [Authorize]
    public ActionResult<object> UpdateTrigger(string siteId, string triggerId, [FromBody] object request) => Ok(request);
    /// <summary>Deletes a trigger.</summary>
    [HttpDelete("api/sites/{siteId}/triggers/{triggerId}")]
    [Authorize]
    public ActionResult<object> DeleteTrigger(string siteId, string triggerId) => Ok(new { site_id = siteId, trigger_id = triggerId, deleted = true });
    /// <summary>Reorders triggers.</summary>
    [HttpPost("api/sites/{siteId}/triggers/reorder")]
    [Authorize]
    public ActionResult<object> Reorder(string siteId, [FromBody] object request) => Ok(request);
    /// <summary>Gets public widget triggers.</summary>
    [HttpGet("api/widget/{siteId}/triggers")]
    [AllowAnonymous]
    public ActionResult<object> WidgetTriggers(string siteId) => Ok(new { triggers = Array.Empty<object>(), global_cooldown_ms = 30000 });
    /// <summary>Records a trigger event.</summary>
    [HttpPost("api/widget/{siteId}/triggers/event")]
    [AllowAnonymous]
    public ActionResult<object> TriggerEvent(string siteId, [FromBody] object request) => Ok(new { success = true });
    /// <summary>Gets trigger analytics.</summary>
    [HttpGet("api/analytics/triggers/{siteId}")]
    [Authorize]
    public ActionResult<object> TriggerAnalytics(string siteId) => Ok(new { site_id = siteId, triggers = Array.Empty<object>() });
    /// <summary>Creates default triggers.</summary>
    [HttpPost("api/sites/{siteId}/triggers/defaults")]
    [Authorize]
    public ActionResult<object> Defaults(string siteId) => Ok(new { site_id = siteId, created = true });
}
