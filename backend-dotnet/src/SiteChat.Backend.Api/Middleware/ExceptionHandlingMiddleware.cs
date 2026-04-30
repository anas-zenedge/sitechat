using Microsoft.Extensions.Options;
using SiteChat.Backend.Api.Configuration;

namespace SiteChat.Backend.Api.Middleware;

/// <summary>
/// Converts unhandled exceptions into safe JSON responses.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IOptions<SiteChatOptions> options)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SiteChatOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Invokes the downstream request pipeline.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that completes when request processing finishes.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled request failure for {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                detail = _options.IsProduction ? "Internal server error" : ex.Message
            }).ConfigureAwait(false);
        }
    }
}
