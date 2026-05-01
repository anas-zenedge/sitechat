using Microsoft.Extensions.Options;
using SiteChat.Backend.Api.Configuration;

namespace SiteChat.Backend.Api.Middleware;

/// <summary>
/// Adds defense-in-depth security headers to API and HTML responses.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IOptions<SiteChatOptions> options)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly SiteChatOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Processes a request and attaches configured security headers.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that completes when the response has been processed.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await _next(context).ConfigureAwait(false);

        if (!_options.Security.EnableSecurityHeaders)
        {
            return;
        }

        context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.TryAdd("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

        if (_options.IsProduction)
        {
            context.Response.Headers.TryAdd("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }

        if (!context.Request.Path.StartsWithSegments("/api") && !string.IsNullOrWhiteSpace(_options.Security.ContentSecurityPolicy))
        {
            context.Response.Headers.TryAdd("Content-Security-Policy", _options.Security.ContentSecurityPolicy);
        }
    }
}
