using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Models;

namespace SiteChat.Backend.Api.Services;

/// <summary>
/// Defines MongoDB operations used by the ASP.NET Core API.
/// </summary>
public interface IMongoSiteChatRepository
{
    /// <summary>Ensures required MongoDB indexes exist.</summary>
    Task EnsureIndexesAsync(CancellationToken cancellationToken);
    /// <summary>Checks whether MongoDB is reachable.</summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
    /// <summary>Gets a user by email.</summary>
    Task<MongoUser?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);
    /// <summary>Gets a user by public or Mongo object identifier.</summary>
    Task<MongoUser?> GetUserByIdAsync(string userId, CancellationToken cancellationToken);
    /// <summary>Gets a user by role.</summary>
    Task<MongoUser?> GetUserByRoleAsync(string role, CancellationToken cancellationToken);
    /// <summary>Lists all users.</summary>
    Task<IReadOnlyList<MongoUser>> GetAllUsersAsync(CancellationToken cancellationToken);
    /// <summary>Creates a user.</summary>
    Task<MongoUser> CreateUserAsync(MongoUser user, CancellationToken cancellationToken);
    /// <summary>Updates a user by identifier.</summary>
    Task<bool> UpdateUserAsync(string userId, UpdateDefinition<MongoUser> update, CancellationToken cancellationToken);
    /// <summary>Updates a user's role by identifier.</summary>
    Task<bool> UpdateUserRoleAsync(string userId, string role, CancellationToken cancellationToken);
    /// <summary>Deletes a user by identifier.</summary>
    Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken);
    /// <summary>Gets a site.</summary>
    Task<MongoSite?> GetSiteAsync(string siteId, CancellationToken cancellationToken);
    /// <summary>Creates a site.</summary>
    Task<MongoSite> CreateSiteAsync(MongoSite site, CancellationToken cancellationToken);
    /// <summary>Lists sites visible by optional owner.</summary>
    Task<IReadOnlyList<MongoSite>> ListSitesAsync(string? userId, IReadOnlyList<string>? siteIds, CancellationToken cancellationToken);
    /// <summary>Updates a site document.</summary>
    Task<bool> UpdateSiteAsync(string siteId, UpdateDefinition<MongoSite> update, CancellationToken cancellationToken);
    /// <summary>Saves a site's typed configuration.</summary>
    Task<bool> SaveSiteConfigAsync(string siteId, SiteConfig config, CancellationToken cancellationToken);
    /// <summary>Deletes a site.</summary>
    Task<bool> DeleteSiteAsync(string siteId, CancellationToken cancellationToken);
    /// <summary>Saves a chat message.</summary>
    Task SaveMessageAsync(string sessionId, string role, string content, string? siteId, IReadOnlyList<SourceDocument>? sources, CancellationToken cancellationToken);
    /// <summary>Gets a conversation by session identifier.</summary>
    Task<MongoConversation?> GetConversationAsync(string sessionId, CancellationToken cancellationToken);
    /// <summary>Lists conversations.</summary>
    Task<(IReadOnlyList<MongoConversation> Items, long Total)> ListConversationsAsync(string? siteId, string? search, int page, int limit, CancellationToken cancellationToken);
    /// <summary>Lists conversations for a set of site identifiers.</summary>
    Task<(IReadOnlyList<MongoConversation> Items, long Total)> ListConversationsForSitesAsync(IReadOnlyList<string> siteIds, string? search, int page, int limit, CancellationToken cancellationToken);
    /// <summary>Deletes conversations by session identifier.</summary>
    Task<long> DeleteConversationsAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken);
    /// <summary>Creates a crawl job.</summary>
    Task<MongoCrawlJob> CreateCrawlJobAsync(string targetUrl, CancellationToken cancellationToken);
    /// <summary>Gets a crawl job by identifier.</summary>
    Task<MongoCrawlJob?> GetCrawlJobAsync(string jobId, CancellationToken cancellationToken);
    /// <summary>Gets the latest crawl job.</summary>
    Task<MongoCrawlJob?> GetLatestCrawlJobAsync(CancellationToken cancellationToken);
    /// <summary>Updates a crawl job.</summary>
    Task UpdateCrawlJobAsync(string jobId, string status, int pagesCrawled, int pagesIndexed, string? error, CancellationToken cancellationToken);
    /// <summary>Saves a crawled page.</summary>
    Task SavePageAsync(string url, string title, string content, int chunkCount, string? siteId, IReadOnlyList<double>? embedding, CancellationToken cancellationToken);
    /// <summary>Gets indexed pages that are available for retrieval.</summary>
    Task<IReadOnlyList<IndexedPage>> GetPagesForRetrievalAsync(string? siteId, CancellationToken cancellationToken);
    /// <summary>Gets aggregate system stats.</summary>
    Task<SystemStats> GetSystemStatsAsync(CancellationToken cancellationToken);
    /// <summary>Gets aggregate system stats for a set of sites.</summary>
    Task<SystemStats> GetSystemStatsForSitesAsync(IReadOnlyList<string> siteIds, CancellationToken cancellationToken);
    /// <summary>Lists indexed pages.</summary>
    Task<IReadOnlyList<BsonDocument>> ListPagesAsync(CancellationToken cancellationToken);
    /// <summary>Deletes an indexed page by URL.</summary>
    Task<bool> DeletePageAsync(string url, CancellationToken cancellationToken);
    /// <summary>Clears operational platform data.</summary>
    Task ClearOperationalDataAsync(CancellationToken cancellationToken);
    /// <summary>Gets a platform white-label config as raw document.</summary>
    Task<BsonDocument?> GetPlatformWhiteLabelAsync(CancellationToken cancellationToken);
    /// <summary>Updates a platform white-label config.</summary>
    Task<BsonDocument> UpdatePlatformWhiteLabelAsync(BsonDocument config, CancellationToken cancellationToken);
    /// <summary>Gets a typed platform white-label config.</summary>
    Task<PlatformWhiteLabelConfig?> GetPlatformWhiteLabelConfigAsync(CancellationToken cancellationToken);
    /// <summary>Updates a typed platform white-label config.</summary>
    Task<PlatformWhiteLabelConfig> UpdatePlatformWhiteLabelConfigAsync(PlatformWhiteLabelConfig config, CancellationToken cancellationToken);
}

/// <summary>
/// Implements the repository and provider pattern for the existing SiteChat MongoDB schema.
/// </summary>
public sealed class MongoSiteChatRepository(IOptions<SiteChatOptions> options, ILogger<MongoSiteChatRepository> logger) : IMongoSiteChatRepository
{
    private readonly SiteChatOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<MongoSiteChatRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Lazy<IMongoDatabase> _database = new(() =>
    {
        var configured = options.Value;
        var client = new MongoClient(configured.MongoDb.Url);
        return client.GetDatabase(configured.MongoDb.Database);
    });

    private IMongoCollection<MongoUser> Users => _database.Value.GetCollection<MongoUser>("users");
    private IMongoCollection<MongoSite> Sites => _database.Value.GetCollection<MongoSite>("sites");
    private IMongoCollection<MongoConversation> Conversations => _database.Value.GetCollection<MongoConversation>("conversations");
    private IMongoCollection<MongoCrawlJob> CrawlJobs => _database.Value.GetCollection<MongoCrawlJob>("crawl_jobs");
    private IMongoCollection<BsonDocument> Pages => _database.Value.GetCollection<BsonDocument>("pages");
    private IMongoCollection<BsonDocument> LongTermMemory => _database.Value.GetCollection<BsonDocument>("long_term_memory");
    private IMongoCollection<BsonDocument> PlatformSettings => _database.Value.GetCollection<BsonDocument>("platform_settings");

    /// <inheritdoc />
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        await Users.Indexes.CreateOneAsync(new CreateIndexModel<MongoUser>(
            Builders<MongoUser>.IndexKeys.Ascending(user => user.Email),
            new CreateIndexOptions { Unique = true }), cancellationToken: cancellationToken).ConfigureAwait(false);
        await Sites.Indexes.CreateOneAsync(new CreateIndexModel<MongoSite>(
            Builders<MongoSite>.IndexKeys.Ascending(site => site.SiteId),
            new CreateIndexOptions { Unique = true }), cancellationToken: cancellationToken).ConfigureAwait(false);
        await Conversations.Indexes.CreateOneAsync(new CreateIndexModel<MongoConversation>(
            Builders<MongoConversation>.IndexKeys.Ascending(conversation => conversation.SessionId)), cancellationToken: cancellationToken).ConfigureAwait(false);
        await Conversations.Indexes.CreateOneAsync(new CreateIndexModel<MongoConversation>(
            Builders<MongoConversation>.IndexKeys.Ascending(conversation => conversation.SiteId)), cancellationToken: cancellationToken).ConfigureAwait(false);
        await CrawlJobs.Indexes.CreateOneAsync(new CreateIndexModel<MongoCrawlJob>(
            Builders<MongoCrawlJob>.IndexKeys.Descending(job => job.CreatedAt)), cancellationToken: cancellationToken).ConfigureAwait(false);
        await Pages.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("url"),
            new CreateIndexOptions { Unique = true }), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _database.Value.RunCommandAsync((Command<BsonDocument>)"{ping:1}", cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MongoDB health check failed");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<MongoUser?> GetUserByEmailAsync(string email, CancellationToken cancellationToken) =>
        await Users.Find(user => user.Email == email).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<MongoUser?> GetUserByIdAsync(string userId, CancellationToken cancellationToken)
    {
        var filters = new List<FilterDefinition<MongoUser>> { Builders<MongoUser>.Filter.Eq(user => user.UserId, userId) };
        if (ObjectId.TryParse(userId, out var objectId))
        {
            filters.Add(Builders<MongoUser>.Filter.Eq(user => user.ObjectId, objectId));
        }

        return await Users.Find(Builders<MongoUser>.Filter.Or(filters)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MongoUser?> GetUserByRoleAsync(string role, CancellationToken cancellationToken) =>
        await Users.Find(user => user.Role == role).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MongoUser>> GetAllUsersAsync(CancellationToken cancellationToken) =>
        await Users.Find(FilterDefinition<MongoUser>.Empty).SortByDescending(user => user.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<MongoUser> CreateUserAsync(MongoUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.UserId = string.IsNullOrWhiteSpace(user.UserId) ? Guid.NewGuid().ToString() : user.UserId;
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await Users.InsertOneAsync(user, cancellationToken: cancellationToken).ConfigureAwait(false);
        return user;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateUserAsync(string userId, UpdateDefinition<MongoUser> update, CancellationToken cancellationToken)
    {
        update = Builders<MongoUser>.Update.Combine(update, Builders<MongoUser>.Update.Set(user => user.UpdatedAt, DateTime.UtcNow));
        var result = await Users.UpdateOneAsync(UserIdFilter(userId), update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }

    /// <inheritdoc />
    public Task<bool> UpdateUserRoleAsync(string userId, string role, CancellationToken cancellationToken) =>
        UpdateUserAsync(userId, Builders<MongoUser>.Update.Set(user => user.Role, role), cancellationToken);

    /// <inheritdoc />
    public async Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken)
    {
        var result = await Users.DeleteOneAsync(UserIdFilter(userId), cancellationToken).ConfigureAwait(false);
        return result.DeletedCount > 0;
    }

    /// <inheritdoc />
    public async Task<MongoSite?> GetSiteAsync(string siteId, CancellationToken cancellationToken) =>
        await Sites.Find(site => site.SiteId == siteId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<MongoSite> CreateSiteAsync(MongoSite site, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(site);
        site.SiteId = string.IsNullOrWhiteSpace(site.SiteId) ? Guid.NewGuid().ToString("N")[..12] : site.SiteId;
        site.CreatedAt = DateTime.UtcNow;
        site.UpdatedAt = DateTime.UtcNow;
        await Sites.InsertOneAsync(site, cancellationToken: cancellationToken).ConfigureAwait(false);
        return site;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MongoSite>> ListSitesAsync(string? userId, IReadOnlyList<string>? siteIds, CancellationToken cancellationToken)
    {
        var filter = FilterDefinition<MongoSite>.Empty;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            filter = Builders<MongoSite>.Filter.Eq(site => site.UserId, userId);
        }
        else if (siteIds is { Count: > 0 })
        {
            filter = Builders<MongoSite>.Filter.In(site => site.SiteId, siteIds);
        }

        return await Sites.Find(filter).SortByDescending(site => site.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateSiteAsync(string siteId, UpdateDefinition<MongoSite> update, CancellationToken cancellationToken)
    {
        update = Builders<MongoSite>.Update.Combine(update, Builders<MongoSite>.Update.Set(site => site.UpdatedAt, DateTime.UtcNow));
        var result = await Sites.UpdateOneAsync(site => site.SiteId == siteId, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }

    /// <inheritdoc />
    public Task<bool> SaveSiteConfigAsync(string siteId, SiteConfig config, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        return UpdateSiteAsync(siteId, Builders<MongoSite>.Update.Set(site => site.Config, SiteConfigDocumentSerializer.Write(config)), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSiteAsync(string siteId, CancellationToken cancellationToken)
    {
        var result = await Sites.DeleteOneAsync(site => site.SiteId == siteId, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount > 0;
    }

    /// <inheritdoc />
    public async Task SaveMessageAsync(string sessionId, string role, string content, string? siteId, IReadOnlyList<SourceDocument>? sources, CancellationToken cancellationToken)
    {
        var sourceDocuments = sources?.Select(source => BsonDocument.Parse(JsonSerializer.Serialize(source, JsonSerializerOptions))).ToList() ?? [];
        var message = new MongoMessage
        {
            Role = role,
            Content = content,
            Sources = sourceDocuments,
            MessageId = $"{sessionId}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Timestamp = DateTime.UtcNow
        };

        var update = Builders<MongoConversation>.Update
            .Push(conversation => conversation.Messages, message)
            .Set(conversation => conversation.UpdatedAt, DateTime.UtcNow)
            .SetOnInsert(conversation => conversation.CreatedAt, DateTime.UtcNow);

        if (!string.IsNullOrWhiteSpace(siteId))
        {
            update = update.Set(conversation => conversation.SiteId, siteId);
        }

        await Conversations.UpdateOneAsync(
            conversation => conversation.SessionId == sessionId,
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MongoConversation?> GetConversationAsync(string sessionId, CancellationToken cancellationToken) =>
        await Conversations.Find(conversation => conversation.SessionId == sessionId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<MongoConversation> Items, long Total)> ListConversationsAsync(string? siteId, string? search, int page, int limit, CancellationToken cancellationToken)
    {
        var filter = FilterDefinition<MongoConversation>.Empty;
        if (!string.IsNullOrWhiteSpace(siteId))
        {
            filter &= Builders<MongoConversation>.Filter.Eq(conversation => conversation.SiteId, siteId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filter &= Builders<MongoConversation>.Filter.Regex("messages.content", new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(search), "i"));
        }

        var total = await Conversations.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        var items = await Conversations.Find(filter)
            .SortByDescending(conversation => conversation.UpdatedAt)
            .Skip(Math.Max(0, page - 1) * limit)
            .Limit(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<MongoConversation> Items, long Total)> ListConversationsForSitesAsync(
        IReadOnlyList<string> siteIds,
        string? search,
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(siteIds);
        if (siteIds.Count == 0)
        {
            return (Array.Empty<MongoConversation>(), 0);
        }

        var filter = Builders<MongoConversation>.Filter.In(conversation => conversation.SiteId, siteIds);
        if (!string.IsNullOrWhiteSpace(search))
        {
            filter &= Builders<MongoConversation>.Filter.Regex("messages.content", new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(search), "i"));
        }

        var total = await Conversations.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        var items = await Conversations.Find(filter)
            .SortByDescending(conversation => conversation.UpdatedAt)
            .Skip(Math.Max(0, page - 1) * limit)
            .Limit(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<long> DeleteConversationsAsync(IReadOnlyList<string> sessionIds, CancellationToken cancellationToken)
    {
        var result = await Conversations.DeleteManyAsync(
            Builders<MongoConversation>.Filter.In(conversation => conversation.SessionId, sessionIds),
            cancellationToken).ConfigureAwait(false);
        return result.DeletedCount;
    }

    /// <inheritdoc />
    public async Task<MongoCrawlJob> CreateCrawlJobAsync(string targetUrl, CancellationToken cancellationToken)
    {
        var job = new MongoCrawlJob { TargetUrl = targetUrl };
        await CrawlJobs.InsertOneAsync(job, cancellationToken: cancellationToken).ConfigureAwait(false);
        return job;
    }

    /// <inheritdoc />
    public async Task<MongoCrawlJob?> GetCrawlJobAsync(string jobId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(jobId, out var objectId))
        {
            return null;
        }

        return await CrawlJobs.Find(job => job.ObjectId == objectId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MongoCrawlJob?> GetLatestCrawlJobAsync(CancellationToken cancellationToken) =>
        await CrawlJobs.Find(FilterDefinition<MongoCrawlJob>.Empty).SortByDescending(job => job.CreatedAt).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task UpdateCrawlJobAsync(string jobId, string status, int pagesCrawled, int pagesIndexed, string? error, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(jobId, out var objectId))
        {
            return;
        }

        var updates = new List<UpdateDefinition<MongoCrawlJob>>
        {
            Builders<MongoCrawlJob>.Update.Set(job => job.Status, status),
            Builders<MongoCrawlJob>.Update.Set(job => job.PagesCrawled, pagesCrawled),
            Builders<MongoCrawlJob>.Update.Set(job => job.PagesIndexed, pagesIndexed),
            Builders<MongoCrawlJob>.Update.Set(job => job.UpdatedAt, DateTime.UtcNow)
        };

        if (!string.IsNullOrWhiteSpace(error))
        {
            updates.Add(Builders<MongoCrawlJob>.Update.Push(job => job.Errors, error));
        }

        await CrawlJobs.UpdateOneAsync(job => job.ObjectId == objectId, Builders<MongoCrawlJob>.Update.Combine(updates), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SavePageAsync(string url, string title, string content, int chunkCount, string? siteId, IReadOnlyList<double>? embedding, CancellationToken cancellationToken)
    {
        var document = new BsonDocument
        {
            ["url"] = url,
            ["title"] = title,
            ["content"] = content,
            ["chunk_count"] = chunkCount,
            ["last_crawled"] = DateTime.UtcNow,
            ["status"] = "indexed",
            ["metadata"] = new BsonDocument { ["site_id"] = siteId ?? string.Empty }
        };

        if (embedding is { Count: > 0 })
        {
            document["embedding"] = new BsonArray(embedding.Select(value => new BsonDouble(value)));
        }

        await Pages.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("url", url),
            new BsonDocument("$set", document).Add("$setOnInsert", new BsonDocument("created_at", DateTime.UtcNow)),
            new UpdateOptions { IsUpsert = true },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IndexedPage>> GetPagesForRetrievalAsync(string? siteId, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Exists("embedding");
        if (!string.IsNullOrWhiteSpace(siteId))
        {
            filter &= Builders<BsonDocument>.Filter.Eq("metadata.site_id", siteId);
        }

        var pages = await Pages.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);
        return pages
            .Select(page => new IndexedPage(
                page.GetValue("url", string.Empty).AsString,
                page.GetValue("title", string.Empty).AsString,
                page.GetValue("content", string.Empty).AsString,
                page.GetValue("chunk_count", 0).ToInt32(),
                page.GetValue("metadata", new BsonDocument()).AsBsonDocument.GetValue("site_id", BsonNull.Value).IsString
                    ? page["metadata"].AsBsonDocument["site_id"].AsString
                    : null,
                page.GetValue("last_crawled", BsonNull.Value).IsValidDateTime ? page["last_crawled"].ToUniversalTime() : (DateTime?)null,
                page.GetValue("embedding", new BsonArray()).AsBsonArray.Select(value => value.ToDouble()).ToArray()))
            .Where(page => page.Embedding.Count > 0)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SystemStats> GetSystemStatsAsync(CancellationToken cancellationToken)
    {
        var totalPages = await Pages.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
        var totalConversations = await Conversations.CountDocumentsAsync(FilterDefinition<MongoConversation>.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
        var latestCrawl = await GetLatestCrawlJobAsync(cancellationToken).ConfigureAwait(false);
        return new SystemStats((int)totalPages, 0, (int)totalConversations, 0, latestCrawl?.CreatedAt);
    }

    /// <inheritdoc />
    public async Task<SystemStats> GetSystemStatsForSitesAsync(IReadOnlyList<string> siteIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(siteIds);
        if (siteIds.Count == 0)
        {
            return new SystemStats(0, 0, 0, 0, null);
        }

        var pageFilter = Builders<BsonDocument>.Filter.In("metadata.site_id", siteIds);
        var conversationFilter = Builders<MongoConversation>.Filter.In(conversation => conversation.SiteId, siteIds);
        var totalPages = await Pages.CountDocumentsAsync(pageFilter, cancellationToken: cancellationToken).ConfigureAwait(false);
        var totalConversations = await Conversations.CountDocumentsAsync(conversationFilter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new SystemStats((int)totalPages, 0, (int)totalConversations, 0, null);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BsonDocument>> ListPagesAsync(CancellationToken cancellationToken) =>
        await Pages.Find(FilterDefinition<BsonDocument>.Empty).Sort(Builders<BsonDocument>.Sort.Descending("last_crawled")).Limit(1000).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<bool> DeletePageAsync(string url, CancellationToken cancellationToken)
    {
        var result = await Pages.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("url", url), cancellationToken).ConfigureAwait(false);
        return result.DeletedCount > 0;
    }

    /// <inheritdoc />
    public async Task ClearOperationalDataAsync(CancellationToken cancellationToken)
    {
        await Conversations.DeleteManyAsync(FilterDefinition<MongoConversation>.Empty, cancellationToken).ConfigureAwait(false);
        await Pages.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken).ConfigureAwait(false);
        await CrawlJobs.DeleteManyAsync(FilterDefinition<MongoCrawlJob>.Empty, cancellationToken).ConfigureAwait(false);
        await LongTermMemory.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<BsonDocument?> GetPlatformWhiteLabelAsync(CancellationToken cancellationToken)
    {
        var config = await PlatformSettings.Find(Builders<BsonDocument>.Filter.Eq("type", "whitelabel")).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        config?.Remove("_id");
        config?.Remove("type");
        return config;
    }

    /// <inheritdoc />
    public async Task<BsonDocument> UpdatePlatformWhiteLabelAsync(BsonDocument config, CancellationToken cancellationToken)
    {
        config["type"] = "whitelabel";
        config["updated_at"] = DateTime.UtcNow;
        await PlatformSettings.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("type", "whitelabel"),
            new BsonDocument("$set", config),
            new UpdateOptions { IsUpsert = true },
            cancellationToken).ConfigureAwait(false);
        config.Remove("_id");
        config.Remove("type");
        return config;
    }

    /// <inheritdoc />
    public async Task<PlatformWhiteLabelConfig?> GetPlatformWhiteLabelConfigAsync(CancellationToken cancellationToken)
    {
        var config = await GetPlatformWhiteLabelAsync(cancellationToken).ConfigureAwait(false);
        return config is null
            ? null
            : JsonSerializer.Deserialize<PlatformWhiteLabelConfig>(config.ToJson(), JsonSerializerOptions);
    }

    /// <inheritdoc />
    public async Task<PlatformWhiteLabelConfig> UpdatePlatformWhiteLabelConfigAsync(PlatformWhiteLabelConfig config, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        var document = BsonDocument.Parse(JsonSerializer.Serialize(config, JsonSerializerOptions));
        var updated = await UpdatePlatformWhiteLabelAsync(document, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<PlatformWhiteLabelConfig>(updated.ToJson(), JsonSerializerOptions) ?? new PlatformWhiteLabelConfig();
    }

    private static FilterDefinition<MongoUser> UserIdFilter(string userId)
    {
        var filters = new List<FilterDefinition<MongoUser>> { Builders<MongoUser>.Filter.Eq(user => user.UserId, userId) };
        if (ObjectId.TryParse(userId, out var objectId))
        {
            filters.Add(Builders<MongoUser>.Filter.Eq(user => user.ObjectId, objectId));
        }

        return Builders<MongoUser>.Filter.Or(filters);
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}
