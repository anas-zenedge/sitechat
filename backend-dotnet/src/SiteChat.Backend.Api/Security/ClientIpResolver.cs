using System.Net;
using Microsoft.Extensions.Options;
using SiteChat.Backend.Api.Configuration;

namespace SiteChat.Backend.Api.Security;

/// <summary>
/// Resolves the effective client IP address for rate limiting and security logging.
/// </summary>
public interface IClientIpResolver
{
    /// <summary>
    /// Resolves the effective client IP for a request.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>The resolved IP address or <c>unknown</c>.</returns>
    string GetClientIp(HttpContext context);
}

/// <summary>
/// Resolves forwarding headers only when the direct peer is a configured trusted proxy.
/// </summary>
public sealed class ClientIpResolver(IOptions<SiteChatOptions> options) : IClientIpResolver
{
    private readonly SiteChatOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public string GetClientIp(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is not null && IsTrustedProxy(remoteIp))
        {
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                var candidate = forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (IPAddress.TryParse(candidate, out var parsed))
                {
                    return parsed.ToString();
                }
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (IPAddress.TryParse(realIp, out var parsedRealIp))
            {
                return parsedRealIp.ToString();
            }
        }

        return remoteIp?.ToString() ?? "unknown";
    }

    private bool IsTrustedProxy(IPAddress remoteIp)
    {
        if (_options.Security.TrustedProxyIps.Count == 0)
        {
            return false;
        }

        return _options.Security.TrustedProxyIps
            .Select(value => IPAddress.TryParse(value, out var parsed) ? parsed : null)
            .Any(parsed => parsed is not null && parsed.Equals(remoteIp));
    }
}
