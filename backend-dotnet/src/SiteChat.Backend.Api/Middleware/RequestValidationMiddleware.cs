namespace SiteChat.Backend.Api.Middleware;

/// <summary>
/// Rejects oversized requests and obvious automated scanner traffic.
/// </summary>
public sealed class RequestValidationMiddleware(RequestDelegate next, ILogger<RequestValidationMiddleware> logger)
{
    private const long MaxContentLength = 10 * 1024 * 1024;
    private static readonly string[] BlockedAgents = ["sqlmap", "nikto", "nessus", "nmap"];
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly ILogger<RequestValidationMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Validates request metadata before passing the request to the application.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that completes when request processing finishes.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.ContentLength > MaxContentLength)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new { detail = "Request body too large" }).ConfigureAwait(false);
            return;
        }

        var userAgent = context.Request.Headers.UserAgent.ToString();
        if (BlockedAgents.Any(agent => userAgent.Contains(agent, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Blocked suspicious user agent {UserAgent} from {RemoteIp}", userAgent, context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { detail = "Access denied" }).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }
}
