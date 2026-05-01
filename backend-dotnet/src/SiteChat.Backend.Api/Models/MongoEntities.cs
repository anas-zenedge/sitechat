using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SiteChat.Backend.Api.Models;

/// <summary>
/// Represents a MongoDB user document.
/// </summary>
public sealed class MongoUser
{
    /// <summary>
    /// Gets or sets the MongoDB object identifier.
    /// </summary>
    [BsonId]
    public ObjectId ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the public user identifier.
    /// </summary>
    [BsonElement("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bcrypt password hash.
    /// </summary>
    [BsonElement("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role name.
    /// </summary>
    [BsonElement("role")]
    public string Role { get; set; } = "user";

    /// <summary>
    /// Gets or sets a value that indicates whether the user must change their password.
    /// </summary>
    [BsonElement("must_change_password")]
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Gets or sets the owner identifier for support agents.
    /// </summary>
    [BsonElement("owner_id")]
    public string? OwnerId { get; set; }

    /// <summary>
    /// Gets or sets assigned site identifiers for support agents.
    /// </summary>
    [BsonElement("assigned_site_ids")]
    public List<string> AssignedSiteIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the update timestamp.
    /// </summary>
    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a MongoDB site document.
/// </summary>
public sealed class MongoSite
{
    /// <summary>
    /// Gets or sets the MongoDB object identifier.
    /// </summary>
    [BsonId]
    public ObjectId ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the public site identifier.
    /// </summary>
    [BsonElement("site_id")]
    public string SiteId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owning user identifier.
    /// </summary>
    [BsonElement("user_id")]
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the site URL.
    /// </summary>
    [BsonElement("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current crawl status.
    /// </summary>
    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Gets or sets the raw site configuration.
    /// </summary>
    [BsonElement("config")]
    public BsonDocument Config { get; set; } = new();

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the update timestamp.
    /// </summary>
    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a persisted chat message.
/// </summary>
public sealed class MongoMessage
{
    /// <summary>
    /// Gets or sets the role.
    /// </summary>
    [BsonElement("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw source documents.
    /// </summary>
    [BsonElement("sources")]
    public List<BsonDocument> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the message metadata.
    /// </summary>
    [BsonElement("metadata")]
    public BsonDocument Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the message timestamp.
    /// </summary>
    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    [BsonElement("message_id")]
    public string MessageId { get; set; } = string.Empty;
}

/// <summary>
/// Represents a MongoDB conversation document.
/// </summary>
public sealed class MongoConversation
{
    /// <summary>
    /// Gets or sets the MongoDB object identifier.
    /// </summary>
    [BsonId]
    public ObjectId ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the session identifier.
    /// </summary>
    [BsonElement("session_id")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the site identifier.
    /// </summary>
    [BsonElement("site_id")]
    public string? SiteId { get; set; }

    /// <summary>
    /// Gets or sets the conversation messages.
    /// </summary>
    [BsonElement("messages")]
    public List<MongoMessage> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the update timestamp.
    /// </summary>
    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    [BsonElement("status")]
    public string Status { get; set; } = "open";

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    [BsonElement("priority")]
    public string Priority { get; set; } = "medium";
}

/// <summary>
/// Represents a MongoDB crawl job document.
/// </summary>
public sealed class MongoCrawlJob
{
    /// <summary>
    /// Gets or sets the MongoDB object identifier.
    /// </summary>
    [BsonId]
    public ObjectId ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the target URL.
    /// </summary>
    [BsonElement("target_url")]
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the job status.
    /// </summary>
    [BsonElement("status")]
    public string Status { get; set; } = "running";

    /// <summary>
    /// Gets or sets the crawled page count.
    /// </summary>
    [BsonElement("pages_crawled")]
    public int PagesCrawled { get; set; }

    /// <summary>
    /// Gets or sets the indexed page count.
    /// </summary>
    [BsonElement("pages_indexed")]
    public int PagesIndexed { get; set; }

    /// <summary>
    /// Gets or sets errors.
    /// </summary>
    [BsonElement("errors")]
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the update timestamp.
    /// </summary>
    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
