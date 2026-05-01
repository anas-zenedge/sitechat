using SiteChat.Backend.Api.Models;

namespace SiteChat.Backend.Api.Services;

/// <summary>
/// Provides MongoDB infrastructure operations used during startup and health checks.
/// </summary>
public interface IMongoInfrastructureRepository
{
    /// <summary>
    /// Ensures required MongoDB indexes exist.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when index creation finishes.</returns>
    Task EnsureIndexesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether MongoDB is reachable.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true" /> if MongoDB is reachable; otherwise, <see langword="false" />.</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Provides user persistence operations.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by email address.
    /// </summary>
    /// <param name="email">The email address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A user, or <see langword="null" /> when no user matches the email.</returns>
    Task<MongoUser?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a user by public or Mongo object identifier.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A user, or <see langword="null" /> when the user does not exist.</returns>
    Task<MongoUser?> GetUserByIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the first user with the specified role.
    /// </summary>
    /// <param name="role">The role name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A user, or <see langword="null" /> when no user has the role.</returns>
    Task<MongoUser?> GetUserByRoleAsync(string role, CancellationToken cancellationToken);

    /// <summary>
    /// Lists all users.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of users.</returns>
    Task<IReadOnlyList<MongoUser>> GetAllUsersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a user.
    /// </summary>
    /// <param name="user">The user document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A created user document.</returns>
    Task<MongoUser> CreateUserAsync(MongoUser user, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a user by identifier.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="update">The user update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true" /> if a user was updated; otherwise, <see langword="false" />.</returns>
    Task<bool> UpdateUserAsync(string userId, UserUpdate update, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a user's role by identifier.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="role">The normalized role name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true" /> if a user was updated; otherwise, <see langword="false" />.</returns>
    Task<bool> UpdateUserRoleAsync(string userId, string role, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a user by identifier.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true" /> if a user was deleted; otherwise, <see langword="false" />.</returns>
    Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken);
}

/// <summary>
/// Provides site persistence operations.
/// </summary>
public interface ISiteRepository
{
    /// <summary>
    /// Gets a site by identifier.
    /// </summary>
    /// <param name="siteId">The site identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A site, or <see langword="null" /> when the site does not exist.</returns>
    Task<MongoSite?> GetSiteAsync(string siteId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a site.
    /// </summary>
    /// <param name="site">The site document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A created site document.</returns>
    Task<MongoSite> CreateSiteAsync(MongoSite site, CancellationToken cancellationToken);

    /// <summary>
    /// Lists sites visible by optional owner or site identifiers.
    /// </summary>
    /// <param name="userId">The optional owner identifier.</param>
    /// <param name="siteIds">The optional site identifiers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of sites.</returns>
    Task<IReadOnlyList<MongoSite>> ListSitesAsync(string? userId, IReadOnlyList<string>? siteIds, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a site's typed configuration.
    /// </summary>
    /// <param name="siteId">The site identifier.</param>
    /// <param name="config">The site configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true" /> if a site was updated; otherwise, <see langword="false" />.</returns>
    Task<bool> SaveSiteConfigAsync(string siteId, SiteConfig config, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a site by identifier.
    /// </summary>
    /// <param name="siteId">The site identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true" /> if a site was deleted; otherwise, <see langword="false" />.</returns>
    Task<bool> DeleteSiteAsync(string siteId, CancellationToken cancellationToken);
}

/// <summary>
/// Provides conversation persistence operations.
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// Saves a chat message.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="role">The message role.</param>
    /// <param name="content">The message content.</param>
    /// <param name="siteId">The optional site identifier.</param>
    /// <param name="sources">The optional source documents.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the message is saved.</returns>
    Task SaveMessageAsync(string sessionId, string role, string content, string? siteId, IReadOnlyList<SourceDocument>? sources, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a conversation by session identifier.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A conversation, or <see langword="null" /> when the conversation does not exist.</returns>
    Task<MongoConversation?> GetConversationAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists conversations.
    /// </summary>
    /// <param name="siteId">The optional site identifier filter.</param>
    /// <param name="search">The optional search query.</param>
    /// <param name="page">The page number.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of conversations and the total count.</returns>
    Task<(IReadOnlyList<MongoConversation> Items, long Total)> ListConversationsAsync(string? siteId, string? search, int page, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Lists conversations for a set of site identifiers.
    /// </summary>
    /// <param name="siteIds">The site identifiers.</param>
    /// <param name="search">The optional search query.</param>
    /// <param name="page">The page number.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A page of conversations and the total count.</returns>
    Task<(IReadOnlyList<MongoConversation> Items, long Total)> ListConversationsForSitesAsync(IReadOnlyList<string> siteIds, string? search, int page, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes conversations by session identifier.
    /// </summary>
    /// <param name="sessionIds">The session identifiers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of deleted conversations.</returns>
    Task<long> DeleteConversationsAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken);
}

/// <summary>
/// Provides crawl job persistence operations.
/// </summary>
public interface ICrawlJobRepository
{
    /// <summary>
    /// Creates a crawl job.
    /// </summary>
    /// <param name="targetUrl">The target URL.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A created crawl job.</returns>
    Task<MongoCrawlJob> CreateCrawlJobAsync(string targetUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a crawl job by identifier.
    /// </summary>
    /// <param name="jobId">The crawl job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A crawl job, or <see langword="null" /> when the job does not exist.</returns>
    Task<MongoCrawlJob?> GetCrawlJobAsync(string jobId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the latest crawl job.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The latest crawl job, or <see langword="null" /> when no jobs exist.</returns>
    Task<MongoCrawlJob?> GetLatestCrawlJobAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates a crawl job.
    /// </summary>
    /// <param name="jobId">The crawl job identifier.</param>
    /// <param name="status">The crawl job status.</param>
    /// <param name="pagesCrawled">The crawled page count.</param>
    /// <param name="pagesIndexed">The indexed page count.</param>
    /// <param name="error">The optional error message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the crawl job is updated.</returns>
    Task UpdateCrawlJobAsync(string jobId, string status, int pagesCrawled, int pagesIndexed, string? error, CancellationToken cancellationToken);
}

/// <summary>
/// Provides page persistence operations.
/// </summary>
public interface IPageRepository
{
    /// <summary>
    /// Saves a crawled page.
    /// </summary>
    /// <param name="url">The page URL.</param>
    /// <param name="title">The page title.</param>
    /// <param name="content">The page content.</param>
    /// <param name="chunkCount">The derived chunk count.</param>
    /// <param name="siteId">The optional site identifier.</param>
    /// <param name="embedding">The optional embedding vector.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the page is saved.</returns>
    Task SavePageAsync(string url, string title, string content, int chunkCount, string? siteId, IReadOnlyList<double>? embedding, CancellationToken cancellationToken);

    /// <summary>
    /// Gets indexed pages available for retrieval.
    /// </summary>
    /// <param name="siteId">The optional site identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of indexed pages.</returns>
    Task<IReadOnlyList<IndexedPage>> GetPagesForRetrievalAsync(string? siteId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists indexed pages.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of indexed page summaries.</returns>
    Task<IReadOnlyList<IndexedPageSummary>> ListPagesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an indexed page by URL.
    /// </summary>
    /// <param name="url">The page URL.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true" /> if a page was deleted; otherwise, <see langword="false" />.</returns>
    Task<bool> DeletePageAsync(string url, CancellationToken cancellationToken);
}

/// <summary>
/// Provides aggregate system operations.
/// </summary>
public interface ISystemRepository
{
    /// <summary>
    /// Gets aggregate system statistics.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A system statistics snapshot.</returns>
    Task<SystemStats> GetSystemStatsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets aggregate system statistics for a set of sites.
    /// </summary>
    /// <param name="siteIds">The site identifiers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A system statistics snapshot.</returns>
    Task<SystemStats> GetSystemStatsForSitesAsync(IReadOnlyList<string> siteIds, CancellationToken cancellationToken);

    /// <summary>
    /// Clears operational platform data.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the data is cleared.</returns>
    Task ClearOperationalDataAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Provides platform white-label configuration persistence operations.
/// </summary>
public interface IPlatformConfigurationRepository
{
    /// <summary>
    /// Gets the typed platform white-label configuration.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A white-label configuration, or <see langword="null" /> when none exists.</returns>
    Task<PlatformWhiteLabelConfig?> GetPlatformWhiteLabelConfigAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates the typed platform white-label configuration.
    /// </summary>
    /// <param name="config">The white-label configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An updated white-label configuration.</returns>
    Task<PlatformWhiteLabelConfig> UpdatePlatformWhiteLabelConfigAsync(PlatformWhiteLabelConfig config, CancellationToken cancellationToken);
}

/// <summary>
/// Represents a typed user update without exposing MongoDB update primitives.
/// </summary>
/// <param name="Name">The optional display name.</param>
/// <param name="PasswordHash">The optional bcrypt password hash.</param>
/// <param name="MustChangePassword">The optional password reset flag.</param>
/// <param name="AssignedSiteIds">The optional assigned site identifiers.</param>
public sealed record UserUpdate(
    string? Name = null,
    string? PasswordHash = null,
    bool? MustChangePassword = null,
    IReadOnlyList<string>? AssignedSiteIds = null);
