namespace SiteChat.Backend.Api.Models;

/// <summary>
/// Represents a source document cited by a chat response.
/// </summary>
public sealed record SourceDocument(string Url, string Title, string ContentPreview, double RelevanceScore);

/// <summary>
/// Represents a chat request from the widget or dashboard.
/// </summary>
public sealed record ChatRequest(string Message, string SessionId, string? UserId = null, string? SiteId = null, bool Stream = false);

/// <summary>
/// Represents a chat response.
/// </summary>
public sealed record ChatResponse(
    string Answer,
    IReadOnlyList<SourceDocument> Sources,
    double Confidence,
    IReadOnlyList<string> FollowUpQuestions,
    string SessionId,
    int? TokensUsed = null,
    bool SuggestHandoff = false,
    string? HandoffReason = null);

/// <summary>
/// Represents a message in a conversation.
/// </summary>
public sealed record MessageDto(string Role, string Content, IReadOnlyList<SourceDocument> Sources, DateTime Timestamp);

/// <summary>
/// Represents conversation history for a session.
/// </summary>
public sealed record ConversationHistory(string SessionId, IReadOnlyList<MessageDto> Messages, DateTime? CreatedAt, DateTime? UpdatedAt);

/// <summary>
/// Represents a crawl request.
/// </summary>
public sealed record CrawlRequest(string Url, int MaxPages = 100, IReadOnlyList<string>? IncludePatterns = null, IReadOnlyList<string>? ExcludePatterns = null);

/// <summary>
/// Represents crawl status.
/// </summary>
public sealed record CrawlStatus(string JobId, string Status, int PagesCrawled, int PagesIndexed, IReadOnlyList<string> Errors, DateTime StartedAt, DateTime? CompletedAt);

/// <summary>
/// Represents crawl creation output.
/// </summary>
public sealed record CrawlResponse(string JobId, string Message, string Status);

/// <summary>
/// Represents a service health summary.
/// </summary>
public sealed record HealthCheckResponse(string Status, string Mongodb, string VectorStore, string Ollama);

/// <summary>
/// Represents system statistics.
/// </summary>
public sealed record SystemStats(int TotalPages, int TotalChunks, int TotalConversations, int TotalMessages, DateTime? LastCrawl);

/// <summary>
/// Represents an indexed page summary returned by management endpoints.
/// </summary>
public sealed record IndexedPageSummary(string Url, string Title, int ChunkCount, DateTime? LastCrawled, string Status);

/// <summary>
/// Represents a user's login credentials.
/// </summary>
public sealed record UserLogin(string Email, string Password);

/// <summary>
/// Represents a user account returned by the API.
/// </summary>
public sealed record UserResponse(string Id, string Email, string Name, string Role, DateTime CreatedAt, IReadOnlyList<string> AssignedSiteIds, bool MustChangePassword);

/// <summary>
/// Represents a bearer token response.
/// </summary>
public sealed record TokenResponse(string AccessToken, string TokenType, UserResponse User);

/// <summary>
/// Represents profile updates for the current user.
/// </summary>
public sealed record ProfileUpdate(string? Name = null, string? CurrentPassword = null, string? NewPassword = null);

/// <summary>
/// Represents an administrator-created site-owner account.
/// </summary>
public sealed record AdminUserCreate(string Email, string Password, string Name);

/// <summary>
/// Represents an administrator update for a site owner.
/// </summary>
public sealed record SiteOwnerUpdate(string? Name = null, string? Password = null);

/// <summary>
/// Represents support-agent creation input.
/// </summary>
public sealed record AgentCreate(string Email, string Password, string Name, IReadOnlyList<string>? AssignedSiteIds = null);

/// <summary>
/// Represents support-agent update input.
/// </summary>
public sealed record AgentUpdate(string? Name = null, IReadOnlyList<string>? AssignedSiteIds = null, string? Password = null);

/// <summary>
/// Represents the appearance configuration for the widget.
/// </summary>
public sealed record SiteAppearanceConfig(
    string PrimaryColor = "#0D9488",
    string ChatTitle = "Chat with us",
    string WelcomeMessage = "Hi! How can I help you today?",
    string? BotAvatarUrl = null,
    string Position = "bottom-right",
    bool HideBranding = false,
    string? CustomBrandingText = null,
    string? CustomBrandingUrl = null);

/// <summary>
/// Represents model behavior settings for a site.
/// </summary>
public sealed record SiteBehaviorConfig(
    string SystemPrompt = "You are a helpful assistant. Answer questions based on the provided context.",
    double Temperature = 0.7,
    int MaxTokens = 500,
    bool ShowSources = true);

/// <summary>
/// Represents lead capture settings.
/// </summary>
public sealed record SiteLeadCaptureConfig(
    bool CollectEmail = false,
    bool EmailRequired = false,
    string EmailPrompt = "Enter your email to continue",
    bool CollectName = false,
    bool NameRequired = false,
    string CaptureTiming = "before_chat",
    int MessagesBeforeCapture = 3);

/// <summary>
/// Represents widget security settings.
/// </summary>
public sealed record SiteSecurityConfig(
    IReadOnlyList<string>? AllowedDomains = null,
    bool EnforceDomainValidation = false,
    bool RequireReferrer = false,
    int RateLimitPerSession = 60);

/// <summary>
/// Represents a quick prompt shown in the widget.
/// </summary>
public sealed record QuickPrompt(string Id, string Text, string? Icon = null, bool Enabled = true);

/// <summary>
/// Represents quick-prompt configuration.
/// </summary>
public sealed record SiteQuickPromptsConfig(IReadOnlyList<QuickPrompt>? Prompts = null, bool Enabled = true, bool ShowAfterResponse = false, int MaxDisplay = 4)
{
    /// <summary>
    /// Gets the default quick-prompt configuration.
    /// </summary>
    public static SiteQuickPromptsConfig Default => new(
        [
            new QuickPrompt(Guid.NewGuid().ToString("N")[..8], "What can you help me with?", "💡"),
            new QuickPrompt(Guid.NewGuid().ToString("N")[..8], "How do I get started?", "🚀"),
            new QuickPrompt(Guid.NewGuid().ToString("N")[..8], "Tell me about pricing", "💰")
        ]);
}

/// <summary>
/// Represents complete widget configuration.
/// </summary>
public sealed record SiteConfig(
    SiteAppearanceConfig? Appearance = null,
    SiteBehaviorConfig? Behavior = null,
    SiteLeadCaptureConfig? LeadCapture = null,
    SiteSecurityConfig? Security = null,
    SiteQuickPromptsConfig? QuickPrompts = null)
{
    /// <summary>
    /// Gets a normalized configuration with default child sections.
    /// </summary>
    /// <returns>A complete configuration object.</returns>
    public SiteConfig Normalize() => this with
    {
        Appearance = Appearance ?? new SiteAppearanceConfig(),
        Behavior = Behavior ?? new SiteBehaviorConfig(),
        LeadCapture = LeadCapture ?? new SiteLeadCaptureConfig(),
        Security = Security ?? new SiteSecurityConfig([]),
        QuickPrompts = QuickPrompts ?? SiteQuickPromptsConfig.Default
    };
}

/// <summary>
/// Represents partial site configuration updates.
/// </summary>
public sealed record SiteConfigUpdate(
    SiteAppearanceConfig? Appearance = null,
    SiteBehaviorConfig? Behavior = null,
    SiteLeadCaptureConfig? LeadCapture = null,
    SiteSecurityConfig? Security = null,
    SiteQuickPromptsConfig? QuickPrompts = null);

/// <summary>
/// Represents a lead capture request.
/// </summary>
public sealed record LeadCreate(string SiteId, string SessionId, string? Email = null, string? Name = null, string Source = "chat", string? Website = null);

/// <summary>
/// Represents a lead list item.
/// </summary>
public sealed record LeadListItem(string Id, string SiteId, string SessionId, string? Email, string? Name, DateTime CapturedAt, string Source);

/// <summary>
/// Represents paginated leads.
/// </summary>
public sealed record LeadListResponse(IReadOnlyList<LeadListItem> Leads, long Total, int Page, int Limit, long TotalPages);

/// <summary>
/// Represents a conversation list item.
/// </summary>
public sealed record ConversationListItem(
    string SessionId,
    string? SiteId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MessageCount,
    string FirstMessage,
    string Status = "open",
    string Priority = "medium",
    IReadOnlyList<string>? Tags = null,
    bool Unread = true,
    string? VisitorName = null,
    string? VisitorEmail = null,
    int? SatisfactionRating = null,
    double? Sentiment = null);

/// <summary>
/// Represents paginated conversations.
/// </summary>
public sealed record ConversationListResponse(IReadOnlyList<ConversationListItem> Conversations, long Total, int Page, int Limit, long TotalPages);

/// <summary>
/// Represents a conversation search result.
/// </summary>
public sealed record ConversationSearchItem(
    string SessionId,
    string? SiteId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MessageCount,
    string FirstMessage,
    string MatchingSnippet);

/// <summary>
/// Represents paginated conversation search output.
/// </summary>
public sealed record ConversationSearchResponse(IReadOnlyList<ConversationSearchItem> Conversations, long Total, int Page, int Limit, long TotalPages, string Query);

/// <summary>
/// Represents a note attached to a conversation.
/// </summary>
public sealed record ConversationNote(string NoteId, string Content, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>
/// Represents conversation statistics.
/// </summary>
public sealed record ConversationStats(int MessageCount, int UserMessages, int AssistantMessages, int PositiveFeedback, int NegativeFeedback, double AvgResponseTimeMs, int? FirstResponseTimeMs = null, int? ResolutionTimeMs = null);

/// <summary>
/// Represents a detailed conversation message.
/// </summary>
public sealed record MessageDetail(string Role, string Content, IReadOnlyList<Dictionary<string, object?>> Sources, DateTime Timestamp, string? Feedback = null, DateTime? FeedbackAt = null, int? ResponseTimeMs = null);

/// <summary>
/// Represents detailed conversation output.
/// </summary>
public sealed record ConversationDetail(
    string SessionId,
    string? SiteId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<MessageDetail> Messages,
    ConversationStats Stats,
    string Status = "open",
    string Priority = "medium",
    IReadOnlyList<string>? Tags = null,
    bool Unread = true,
    string? VisitorName = null,
    string? VisitorEmail = null,
    string? PageUrl = null,
    IReadOnlyList<ConversationNote>? Notes = null);

/// <summary>
/// Represents bulk conversation deletion input.
/// </summary>
public sealed record BulkDeleteRequest(IReadOnlyList<string> SessionIds);

/// <summary>
/// Represents bulk deletion output.
/// </summary>
public sealed record BulkDeleteResponse(long DeletedCount, string Message);

/// <summary>
/// Represents export request input.
/// </summary>
public sealed record ExportRequest(IReadOnlyList<string>? SessionIds = null, string? SiteId = null, string Format = "json");

/// <summary>
/// Represents status update input.
/// </summary>
public sealed record UpdateStatusRequest(string Status);

/// <summary>
/// Represents priority update input.
/// </summary>
public sealed record UpdatePriorityRequest(string Priority);

/// <summary>
/// Represents tag update input.
/// </summary>
public sealed record UpdateTagsRequest(IReadOnlyList<string> Tags);

/// <summary>
/// Represents note creation input.
/// </summary>
public sealed record AddNoteRequest(string Content);

/// <summary>
/// Represents note update input.
/// </summary>
public sealed record UpdateNoteRequest(string Content);

/// <summary>
/// Represents visitor update input.
/// </summary>
public sealed record UpdateVisitorRequest(string? VisitorName = null, string? VisitorEmail = null);

/// <summary>
/// Represents rating input.
/// </summary>
public sealed record SetRatingRequest(int Rating);

/// <summary>
/// Represents auto-close input.
/// </summary>
public sealed record AutoCloseRequest(int DaysInactive = 7);

/// <summary>
/// Represents auto-close output.
/// </summary>
public sealed record AutoCloseResponse(long ClosedCount, string Message);

/// <summary>
/// Represents platform white-label settings.
/// </summary>
public sealed record PlatformWhiteLabelConfig(string? LogoUrl = null, string? BrandName = null, string? PrimaryColor = null, bool HideSiteChatBranding = false);

/// <summary>
/// Represents setup request input for an embeddable site.
/// </summary>
public sealed record SetupRequest(string Url, string? Name = null);

/// <summary>
/// Represents setup output for an embeddable site.
/// </summary>
public sealed record SetupResponse(string SiteId, string EmbedCode, string ScriptUrl, string Message);

/// <summary>
/// Represents Q&A training data.
/// </summary>
public sealed record QAPair(string Id, string SiteId, string Question, string Answer, bool Enabled, int UseCount, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>
/// Represents Q&A creation input.
/// </summary>
public sealed record QAPairCreate(string Question, string Answer, bool Enabled = true);

/// <summary>
/// Represents Q&A update input.
/// </summary>
public sealed record QAPairUpdate(string? Question = null, string? Answer = null, bool? Enabled = null);

/// <summary>
/// Represents Q&A list output.
/// </summary>
public sealed record QAPairListResponse(IReadOnlyList<QAPair> Items, long Total);

/// <summary>
/// Represents a simple API success response.
/// </summary>
public sealed record ApiMessageResponse(string Message);
