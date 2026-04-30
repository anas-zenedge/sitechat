using System.Net;
using System.Net.Sockets;

namespace SiteChat.Backend.Api.Security;

/// <summary>
/// Validates outbound crawler URLs before the backend makes network requests.
/// </summary>
public interface ICrawlerUrlValidator
{
    /// <summary>
    /// Validates whether a URL is safe for server-side crawling.
    /// </summary>
    /// <param name="url">The URL candidate.</param>
    /// <param name="cancellationToken">A token that cancels DNS resolution.</param>
    /// <returns>A validation result.</returns>
    Task<CrawlerUrlValidationResult> ValidateAsync(string? url, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents crawler URL validation output.
/// </summary>
/// <param name="IsValid"><see langword="true" /> if the URL is valid; otherwise, <see langword="false" />.</param>
/// <param name="Uri">The parsed absolute URI.</param>
/// <param name="ErrorMessage">The validation error message.</param>
public sealed record CrawlerUrlValidationResult(bool IsValid, Uri? Uri, string ErrorMessage);

/// <summary>
/// Blocks non-HTTP schemes and private, loopback, link-local, multicast, and reserved destinations.
/// </summary>
public sealed class CrawlerUrlValidator : ICrawlerUrlValidator
{
    /// <inheritdoc />
    public async Task<CrawlerUrlValidationResult> ValidateAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return new CrawlerUrlValidationResult(false, null, "URL must be absolute");
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return new CrawlerUrlValidationResult(false, uri, "Only http and https URLs can be crawled");
        }

        if (uri.HostNameType is UriHostNameType.Unknown)
        {
            return new CrawlerUrlValidationResult(false, uri, "URL host is invalid");
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            return new CrawlerUrlValidationResult(false, uri, "URL host could not be resolved");
        }

        if (addresses.Length == 0)
        {
            return new CrawlerUrlValidationResult(false, uri, "URL host resolved to no addresses");
        }

        if (addresses.Any(IsUnsafeAddress))
        {
            return new CrawlerUrlValidationResult(false, uri, "URL resolves to a private, loopback, link-local, multicast, or reserved address");
        }

        return new CrawlerUrlValidationResult(true, uri, string.Empty);
    }

    private static bool IsUnsafeAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal
                || address.IsIPv6Multicast
                || address.IsIPv6SiteLocal
                || address.Equals(IPAddress.IPv6None)
                || address.Equals(IPAddress.IPv6Any);
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 0
            || bytes[0] == 10
            || bytes[0] == 127
            || bytes[0] >= 224
            || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0)
            || (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2)
            || (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19))
            || (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100)
            || (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
    }
}
