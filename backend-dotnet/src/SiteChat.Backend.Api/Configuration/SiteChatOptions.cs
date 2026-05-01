using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SiteChat.Backend.Api.Configuration;

/// <summary>
/// Provides strongly typed application configuration for the SiteChat ASP.NET Core backend.
/// </summary>
public sealed class SiteChatOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "SiteChat";

    /// <summary>
    /// Gets or sets the application display name.
    /// </summary>
    [Required]
    public string AppName { get; set; } = "SiteChat";

    /// <summary>
    /// Gets or sets the deployment environment.
    /// </summary>
    [Required]
    public string Environment { get; set; } = "development";

    /// <summary>
    /// Gets or sets a value that indicates whether debug behavior is enabled.
    /// </summary>
    public bool Debug { get; set; }

    /// <summary>
    /// Gets or sets the externally visible site URL.
    /// </summary>
    [Required]
    public string SiteUrl { get; set; } = "http://localhost:8000";

    /// <summary>
    /// Gets the security options.
    /// </summary>
    [Required]
    public SecurityOptions Security { get; init; } = new();

    /// <summary>
    /// Gets the MongoDB options.
    /// </summary>
    [Required]
    public MongoOptions MongoDb { get; init; } = new();

    /// <summary>
    /// Gets the JWT options.
    /// </summary>
    [Required]
    public JwtOptions Jwt { get; init; } = new();

    /// <summary>
    /// Gets the password policy options.
    /// </summary>
    [Required]
    public PasswordPolicyOptions PasswordPolicy { get; init; } = new();

    /// <summary>
    /// Gets the LLM and RAG options.
    /// </summary>
    [Required]
    public RagOptions Rag { get; init; } = new();

    /// <summary>
    /// Gets the crawler options.
    /// </summary>
    [Required]
    public CrawlOptions Crawl { get; init; } = new();

    /// <summary>
    /// Gets the rate-limit options.
    /// </summary>
    [Required]
    public RateLimitOptions RateLimit { get; init; } = new();

    /// <summary>
    /// Gets the static file hosting options.
    /// </summary>
    [Required]
    public StaticFileOptions StaticFiles { get; init; } = new();

    /// <summary>
    /// Gets a value that indicates whether production hardening should apply.
    /// </summary>
    public bool IsProduction => string.Equals(Environment, "production", StringComparison.OrdinalIgnoreCase) && !Debug;

    /// <summary>
    /// Checks whether an origin is allowed by the configured allowlist or regex.
    /// </summary>
    /// <param name="origin">The Origin header value.</param>
    /// <returns><see langword="true" /> if the origin is allowed; otherwise, <see langword="false" />.</returns>
    public bool IsCorsOriginAllowed(string origin)
    {
        if (Security.CorsOrigins.Contains("*"))
        {
            return true;
        }

        if (Security.CorsOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(Security.CorsOriginRegex)
            && Regex.IsMatch(origin, Security.CorsOriginRegex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

/// <summary>
/// Provides security-related configuration.
/// </summary>
public sealed class SecurityOptions
{
    /// <summary>
    /// Gets or sets the allowed CORS origins.
    /// </summary>
    public List<string> CorsOrigins { get; set; } =
    [
        "http://localhost:3000",
        "http://localhost:8000",
        "http://127.0.0.1:8000",
        "http://localhost:8015",
        "http://127.0.0.1:8015",
        "http://localhost:8012",
        "http://127.0.0.1:8012",
        "http://127.0.0.1",
        "http://localhost"
    ];

    /// <summary>
    /// Gets or sets a value that indicates whether credentialed CORS requests are allowed.
    /// </summary>
    public bool CorsAllowCredentials { get; set; } = true;

    /// <summary>
    /// Gets or sets the optional CORS origin regular expression.
    /// </summary>
    public string? CorsOriginRegex { get; set; } = @"^https?://127\.0\.0\.1(?::\d+)?$";

    /// <summary>
    /// Gets or sets the allowed host names for production deployments.
    /// </summary>
    public List<string> TrustedHosts { get; set; } = ["localhost", "127.0.0.1"];

    /// <summary>
    /// Gets or sets the reverse proxy IP addresses whose forwarding headers may be trusted.
    /// </summary>
    public List<string> TrustedProxyIps { get; set; } = [];

    /// <summary>
    /// Gets or sets a value that indicates whether security headers are sent.
    /// </summary>
    public bool EnableSecurityHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets the Content Security Policy applied to non-API HTML responses.
    /// </summary>
    public string? ContentSecurityPolicy { get; set; }
}

/// <summary>
/// Provides MongoDB connection configuration.
/// </summary>
public sealed class MongoOptions
{
    /// <summary>
    /// Gets or sets the MongoDB connection string.
    /// </summary>
    [Required]
    public string Url { get; set; } = "mongodb://localhost:27017";

    /// <summary>
    /// Gets or sets the MongoDB database name.
    /// </summary>
    [Required]
    public string Database { get; set; } = "sitechat";
}

/// <summary>
/// Provides JWT bearer token configuration.
/// </summary>
public sealed class JwtOptions
{
    private static readonly HashSet<string> InsecureDefaults = new(StringComparer.Ordinal)
    {
        "your-super-secret-key-change-in-production",
        "CHANGE-THIS-SECRET-IN-PRODUCTION",
        "GENERATE-A-64-CHARACTER-SECRET-KEY-HERE",
        "secret",
        "changeme",
        string.Empty
    };

    /// <summary>
    /// Gets or sets the token signing secret.
    /// </summary>
    [Required]
    public string Secret { get; set; } = "CHANGE-THIS-SECRET-IN-PRODUCTION";

    /// <summary>
    /// Gets or sets the token expiration in hours.
    /// </summary>
    [Range(1, 24 * 30)]
    public int ExpireHours { get; set; } = 24;

    /// <summary>
    /// Gets a value that indicates whether the configured secret is suitable for production.
    /// </summary>
    public bool IsSecure => Secret.Length >= 32 && !InsecureDefaults.Contains(Secret);
}

/// <summary>
/// Provides password validation settings.
/// </summary>
public sealed class PasswordPolicyOptions
{
    /// <summary>
    /// Gets or sets the minimum password length.
    /// </summary>
    [Range(8, 256)]
    public int MinimumLength { get; set; } = 8;

    /// <summary>
    /// Gets or sets a value that indicates whether uppercase/lowercase/digit/special complexity is required.
    /// </summary>
    public bool RequireComplexity { get; set; } = true;
}

/// <summary>
/// Provides RAG and LLM configuration.
/// </summary>
public sealed class RagOptions
{
    /// <summary>
     /// Gets or sets the LLM provider name.
     /// </summary>
    [Required]
    public string LlmProvider { get; set; } = "openrouter";

    /// <summary>
     /// Gets or sets the LLM model name.
     /// </summary>
    [Required]
    public string LlmModel { get; set; } = "openai/gpt-4o-mini";

    /// <summary>
     /// Gets or sets the embeddings model name.
     /// </summary>
    [Required]
    public string EmbeddingModel { get; set; } = "openai/text-embedding-3-small";

    /// <summary>
    /// Gets or sets the OpenRouter base URL.
    /// </summary>
    [Required]
    public string OpenRouterBaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    /// <summary>
    /// Gets or sets the OpenRouter API key.
    /// </summary>
    public string OpenRouterApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional OpenRouter referer header value.
    /// </summary>
    public string? OpenRouterReferer { get; set; }

    /// <summary>
    /// Gets or sets the optional OpenRouter application title header value.
    /// </summary>
    public string? OpenRouterTitle { get; set; }

    /// <summary>
     /// Gets or sets the LLM temperature.
     /// </summary>
    [Range(0.0, 2.0)]
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets the maximum response token count.
    /// </summary>
    [Range(1, 8000)]
    public int MaxTokens { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the number of retrieved documents used for RAG context.
    /// </summary>
    [Range(1, 50)]
    public int RetrievalK { get; set; } = 3;
}

/// <summary>
/// Provides crawler configuration.
/// </summary>
public sealed class CrawlOptions
{
    /// <summary>
    /// Gets or sets the maximum pages per crawl.
    /// </summary>
    [Range(1, 1000)]
    public int MaxPages { get; set; } = 100;

    /// <summary>
    /// Gets or sets the delay between crawl requests in seconds.
    /// </summary>
    [Range(0, 60)]
    public double DelaySeconds { get; set; } = 1.0;
}

/// <summary>
/// Provides request rate-limiting configuration.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>
    /// Gets or sets the request count allowed in each window.
    /// </summary>
    [Range(1, 10000)]
    public int Requests { get; set; } = 20;

    /// <summary>
    /// Gets or sets the rate-limit window in seconds.
    /// </summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;
}

/// <summary>
/// Provides static frontend hosting configuration.
/// </summary>
public sealed class StaticFileOptions
{
    /// <summary>
    /// Gets or sets the optional frontend root path.
    /// </summary>
    public string? FrontendRoot { get; set; }
}

/// <summary>
/// Contains authorization policy names used by controllers.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Gets the admin-only policy name.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Gets the policy name that allows administrators and site owners.
    /// </summary>
    public const string AdminOrUser = "AdminOrUser";
}

/// <summary>
/// Contains role names stored in MongoDB and JWT claims.
/// </summary>
public static class UserRoles
{
    /// <summary>
    /// Gets the administrator role name.
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    /// Gets the site-owner role name.
    /// </summary>
    public const string User = "user";

    /// <summary>
    /// Gets the support-agent role name.
    /// </summary>
    public const string Agent = "agent";
}
