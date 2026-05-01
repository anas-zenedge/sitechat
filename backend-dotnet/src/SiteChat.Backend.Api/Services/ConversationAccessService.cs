using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Models;

namespace SiteChat.Backend.Api.Services;

/// <summary>
/// Provides conversation queries and mutations scoped to the current user's site access.
/// </summary>
public interface IConversationAccessService
{
    /// <summary>
    /// Checks whether a user can access a site.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <param name="siteId">The site identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true" /> if the user can access the site; otherwise, <see langword="false" />.</returns>
    Task<bool> CanAccessSiteAsync(MongoUser user, string siteId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a conversation when it is visible to the specified user.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A conversation, or <see langword="null" /> when it is missing or inaccessible.</returns>
    Task<MongoConversation?> GetConversationAsync(MongoUser user, string sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists conversations visible to the specified user.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <param name="siteId">The optional site identifier filter.</param>
    /// <param name="search">The optional search query.</param>
    /// <param name="page">The page number.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of accessible conversations.</returns>
    Task<(IReadOnlyList<MongoConversation> Items, long Total)> ListConversationsAsync(
        MongoUser user,
        string? siteId,
        string? search,
        int page,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes accessible conversations for the specified user.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <param name="sessionIds">The session identifiers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of deleted conversations.</returns>
    Task<long> DeleteConversationsAsync(MongoUser user, IReadOnlyList<string> sessionIds, CancellationToken cancellationToken);

    /// <summary>
    /// Gets system statistics visible to the specified user.
    /// </summary>
    /// <param name="user">The current user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A scoped system statistics snapshot.</returns>
    Task<SystemStats> GetSystemStatsAsync(MongoUser user, CancellationToken cancellationToken);
}

/// <summary>
/// Implements conversation access rules using site ownership and assignment rules.
/// </summary>
public sealed class ConversationAccessService(
    IConversationRepository conversationRepository,
    ISiteRepository siteRepository,
    ISystemRepository systemRepository,
    ISiteAccessService siteAccessService) : IConversationAccessService
{
    private readonly IConversationRepository _conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
    private readonly ISiteRepository _siteRepository = siteRepository ?? throw new ArgumentNullException(nameof(siteRepository));
    private readonly ISystemRepository _systemRepository = systemRepository ?? throw new ArgumentNullException(nameof(systemRepository));
    private readonly ISiteAccessService _siteAccessService = siteAccessService ?? throw new ArgumentNullException(nameof(siteAccessService));

    /// <inheritdoc />
    public async Task<bool> CanAccessSiteAsync(MongoUser user, string siteId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Role == UserRoles.Admin)
        {
            return true;
        }

        var site = await _siteRepository.GetSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        return site is not null && _siteAccessService.CanViewSite(user, site);
    }

    /// <inheritdoc />
    public async Task<MongoConversation?> GetConversationAsync(MongoUser user, string sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var conversation = await _conversationRepository.GetConversationAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (conversation is null)
        {
            return null;
        }

        if (user.Role == UserRoles.Admin)
        {
            return conversation;
        }

        if (string.IsNullOrWhiteSpace(conversation.SiteId))
        {
            return null;
        }

        var site = await _siteRepository.GetSiteAsync(conversation.SiteId, cancellationToken).ConfigureAwait(false);
        return site is not null && _siteAccessService.CanViewSite(user, site) ? conversation : null;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<MongoConversation> Items, long Total)> ListConversationsAsync(
        MongoUser user,
        string? siteId,
        string? search,
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.Role == UserRoles.Admin)
        {
            return await _conversationRepository.ListConversationsAsync(siteId, search, page, limit, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(siteId))
        {
            return await CanAccessSiteAsync(user, siteId, cancellationToken).ConfigureAwait(false)
                ? await _conversationRepository.ListConversationsAsync(siteId, search, page, limit, cancellationToken).ConfigureAwait(false)
                : (Array.Empty<MongoConversation>(), 0L);
        }

        var accessibleSiteIds = await GetAccessibleSiteIdsAsync(user, cancellationToken).ConfigureAwait(false);
        return accessibleSiteIds.Count == 0
            ? (Array.Empty<MongoConversation>(), 0L)
            : await _conversationRepository.ListConversationsForSitesAsync(accessibleSiteIds, search, page, limit, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> DeleteConversationsAsync(MongoUser user, IReadOnlyList<string> sessionIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(sessionIds);

        var accessibleSessionIds = new List<string>();
        foreach (var sessionId in sessionIds.Distinct(StringComparer.Ordinal))
        {
            if (await GetConversationAsync(user, sessionId, cancellationToken).ConfigureAwait(false) is not null)
            {
                accessibleSessionIds.Add(sessionId);
            }
        }

        return accessibleSessionIds.Count == 0
            ? 0
            : await _conversationRepository.DeleteConversationsAsync(accessibleSessionIds, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SystemStats> GetSystemStatsAsync(MongoUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Role == UserRoles.Admin)
        {
            return await _systemRepository.GetSystemStatsAsync(cancellationToken).ConfigureAwait(false);
        }

        var accessibleSiteIds = await GetAccessibleSiteIdsAsync(user, cancellationToken).ConfigureAwait(false);
        return await _systemRepository.GetSystemStatsForSitesAsync(accessibleSiteIds, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<string>> GetAccessibleSiteIdsAsync(MongoUser user, CancellationToken cancellationToken)
    {
        IReadOnlyList<MongoSite> sites = user.Role switch
        {
            UserRoles.User => await _siteRepository.ListSitesAsync(MongoIdentifiers.GetPublicId(user), null, cancellationToken).ConfigureAwait(false),
            UserRoles.Agent => await _siteRepository.ListSitesAsync(null, user.AssignedSiteIds, cancellationToken).ConfigureAwait(false),
            _ => Array.Empty<MongoSite>()
        };

        return sites.Select(site => site.SiteId).Distinct(StringComparer.Ordinal).ToList();
    }
}
