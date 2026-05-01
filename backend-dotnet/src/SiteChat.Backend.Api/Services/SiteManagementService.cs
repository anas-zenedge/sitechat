using SiteChat.Backend.Api.Models;

namespace SiteChat.Backend.Api.Services;

/// <summary>
/// Provides site provisioning and configuration operations.
/// </summary>
public interface ISiteManagementService
{
    /// <summary>
    /// Creates a site owned by the specified user.
    /// </summary>
    /// <param name="owner">The owning user.</param>
    /// <param name="request">The setup request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A created site document.</returns>
    Task<MongoSite> CreateSiteAsync(MongoUser owner, SetupRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the normalized configuration for a site.
    /// </summary>
    /// <param name="siteId">The site identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A normalized site configuration, or <see langword="null" /> when the site does not exist.</returns>
    Task<SiteConfig?> GetConfigAsync(string siteId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a site's configuration.
    /// </summary>
    /// <param name="siteId">The site identifier.</param>
    /// <param name="update">The configuration update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A normalized site configuration, or <see langword="null" /> when the site does not exist.</returns>
    Task<SiteConfig?> UpdateConfigAsync(string siteId, SiteConfigUpdate update, CancellationToken cancellationToken);

    /// <summary>
    /// Resets a site's configuration to defaults.
    /// </summary>
    /// <param name="siteId">The site identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A normalized site configuration, or <see langword="null" /> when the site does not exist.</returns>
    Task<SiteConfig?> ResetConfigAsync(string siteId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a site's quick prompts.
    /// </summary>
    /// <param name="siteId">The site identifier.</param>
    /// <param name="quickPrompts">The quick-prompt configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A quick-prompt configuration, or <see langword="null" /> when the site does not exist.</returns>
    Task<SiteQuickPromptsConfig?> UpdateQuickPromptsAsync(string siteId, SiteQuickPromptsConfig quickPrompts, CancellationToken cancellationToken);
}

/// <summary>
/// Implements site provisioning and configuration persistence behavior.
/// </summary>
public sealed class SiteManagementService(ISiteRepository siteRepository) : ISiteManagementService
{
    private readonly ISiteRepository _siteRepository = siteRepository ?? throw new ArgumentNullException(nameof(siteRepository));

    /// <inheritdoc />
    public Task<MongoSite> CreateSiteAsync(MongoUser owner, SetupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(request);

        return _siteRepository.CreateSiteAsync(new MongoSite
        {
            UserId = MongoIdentifiers.GetPublicId(owner),
            Url = request.Url,
            Name = request.Name ?? request.Url,
            Status = "pending"
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SiteConfig?> GetConfigAsync(string siteId, CancellationToken cancellationToken)
    {
        var site = await _siteRepository.GetSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        return site is null ? null : ReadConfig(site).Normalize();
    }

    /// <inheritdoc />
    public async Task<SiteConfig?> UpdateConfigAsync(string siteId, SiteConfigUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        var site = await _siteRepository.GetSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        if (site is null)
        {
            return null;
        }

        var current = ReadConfig(site).Normalize();
        var merged = current with
        {
            Appearance = update.Appearance ?? current.Appearance,
            Behavior = update.Behavior ?? current.Behavior,
            LeadCapture = update.LeadCapture ?? current.LeadCapture,
            Security = update.Security ?? current.Security,
            QuickPrompts = update.QuickPrompts ?? current.QuickPrompts
        };

        await _siteRepository.SaveSiteConfigAsync(siteId, merged, cancellationToken).ConfigureAwait(false);
        return merged.Normalize();
    }

    /// <inheritdoc />
    public async Task<SiteConfig?> ResetConfigAsync(string siteId, CancellationToken cancellationToken)
    {
        var site = await _siteRepository.GetSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        if (site is null)
        {
            return null;
        }

        var defaultConfig = new SiteConfig().Normalize();
        await _siteRepository.SaveSiteConfigAsync(siteId, defaultConfig, cancellationToken).ConfigureAwait(false);
        return defaultConfig;
    }

    /// <inheritdoc />
    public async Task<SiteQuickPromptsConfig?> UpdateQuickPromptsAsync(string siteId, SiteQuickPromptsConfig quickPrompts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quickPrompts);
        var site = await _siteRepository.GetSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        if (site is null)
        {
            return null;
        }

        var config = ReadConfig(site).Normalize() with { QuickPrompts = quickPrompts };
        await _siteRepository.SaveSiteConfigAsync(siteId, config, cancellationToken).ConfigureAwait(false);
        return quickPrompts;
    }

    private static SiteConfig ReadConfig(MongoSite site) =>
        site.Config.ElementCount == 0 ? new SiteConfig() : SiteConfigDocumentSerializer.Read(site.Config);
}
