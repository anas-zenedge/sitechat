using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Api.Controllers;

/// <summary>
/// Provides site management and public widget-configuration endpoints.
/// </summary>
[ApiController]
[Route("api/sites")]
public sealed class SitesController(
    IMongoSiteChatRepository repository,
    ISiteAccessService accessService,
    ISiteManagementService siteManagementService) : SiteChatControllerBase
{
    private readonly IMongoSiteChatRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly ISiteAccessService _accessService = accessService ?? throw new ArgumentNullException(nameof(accessService));
    private readonly ISiteManagementService _siteManagementService = siteManagementService ?? throw new ArgumentNullException(nameof(siteManagementService));

    /// <summary>Lists sites for the current user.</summary>
    [HttpGet("")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<object>>> ListAsync(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        var ownerId = user!.Role == UserRoles.User ? MongoIdentifiers.GetPublicId(user) : null;
        var siteIds = user.Role == UserRoles.Agent ? user.AssignedSiteIds : null;
        var sites = await _repository.ListSitesAsync(ownerId, siteIds, cancellationToken).ConfigureAwait(false);
        return Ok(sites.Select(ToSiteListItem).ToList());
    }

    /// <summary>Gets a site by identifier.</summary>
    [HttpGet("{siteId}")]
    [Authorize]
    public async Task<ActionResult<object>> GetAsync(string siteId, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        var site = await _repository.GetSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        if (site is null)
        {
            return NotFound(new { detail = "Site not found" });
        }

        return _accessService.CanViewSite(user!, site) ? Ok(ToSiteListItem(site)) : Forbid();
    }

    /// <summary>Deletes a site.</summary>
    [HttpDelete("{siteId}")]
    [Authorize]
    public async Task<ActionResult<ApiMessageResponse>> DeleteAsync(string siteId, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        var site = await _repository.GetSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        if (site is null)
        {
            return NotFound(new { detail = "Site not found" });
        }

        if (!_accessService.CanManageSite(user!, site))
        {
            return Forbid();
        }

        await _repository.DeleteSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        return Ok(new ApiMessageResponse($"Site {siteId} deleted successfully"));
    }

    /// <summary>Gets public site configuration for the widget.</summary>
    [HttpGet("{siteId}/config")]
    [AllowAnonymous]
    public async Task<ActionResult<SiteConfig>> GetConfigAsync(string siteId, CancellationToken cancellationToken)
    {
        var config = await _siteManagementService.GetConfigAsync(siteId, cancellationToken).ConfigureAwait(false);
        return config is null ? NotFound(new { detail = "Site not found" }) : Ok(config);
    }

    /// <summary>Updates site configuration.</summary>
    [HttpPut("{siteId}/config")]
    [Authorize]
    public async Task<ActionResult<SiteConfig>> UpdateConfigAsync(string siteId, [FromBody] SiteConfigUpdate update, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        var site = await _repository.GetSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        if (site is null)
        {
            return NotFound(new { detail = "Site not found" });
        }

        if (user is null || !_accessService.CanManageSite(user, site))
        {
            return Forbid();
        }

        var updated = await _siteManagementService.UpdateConfigAsync(siteId, update, cancellationToken).ConfigureAwait(false);
        return updated is null ? NotFound(new { detail = "Site not found" }) : Ok(updated);
    }

    /// <summary>Resets site configuration to defaults.</summary>
    [HttpPost("{siteId}/config/reset")]
    [Authorize]
    public async Task<ActionResult<SiteConfig>> ResetConfigAsync(string siteId, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        var site = await _repository.GetSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        if (site is null)
        {
            return NotFound(new { detail = "Site not found" });
        }

        if (!_accessService.CanManageSite(user!, site))
        {
            return Forbid();
        }

        var defaultConfig = await _siteManagementService.ResetConfigAsync(siteId, cancellationToken).ConfigureAwait(false);
        return defaultConfig is null ? NotFound(new { detail = "Site not found" }) : Ok(defaultConfig);
    }

    /// <summary>Updates quick prompts.</summary>
    [HttpPut("{siteId}/quick-prompts")]
    [Authorize]
    public async Task<ActionResult<SiteQuickPromptsConfig>> UpdateQuickPromptsAsync(string siteId, [FromBody] SiteQuickPromptsConfig request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        var site = await _repository.GetSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        if (site is null)
        {
            return NotFound(new { detail = "Site not found" });
        }

        if (!_accessService.CanManageSite(user!, site))
        {
            return Forbid();
        }

        var updated = await _siteManagementService.UpdateQuickPromptsAsync(siteId, request, cancellationToken).ConfigureAwait(false);
        return updated is null ? NotFound(new { detail = "Site not found" }) : Ok(updated);
    }

    /// <summary>Gets public quick prompts.</summary>
    [HttpGet("{siteId}/quick-prompts")]
    [AllowAnonymous]
    public async Task<ActionResult<SiteQuickPromptsConfig>> GetQuickPromptsAsync(string siteId, CancellationToken cancellationToken)
    {
        var config = await _siteManagementService.GetConfigAsync(siteId, cancellationToken).ConfigureAwait(false);
        return config is null ? NotFound(new { detail = "Site not found" }) : Ok(config.QuickPrompts);
    }

    private static object ToSiteListItem(MongoSite site) => new
    {
        site_id = site.SiteId,
        name = site.Name,
        url = site.Url,
        user_id = site.UserId,
        status = site.Status,
        pages_crawled = 0,
        pages_indexed = 0,
        created_at = site.CreatedAt
    };

}
