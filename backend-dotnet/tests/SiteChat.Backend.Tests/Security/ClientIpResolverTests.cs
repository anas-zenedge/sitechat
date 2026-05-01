using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Security;

namespace SiteChat.Backend.Tests.Security;

/// <summary>
/// Tests trusted proxy client IP resolution.
/// </summary>
[TestClass]
public sealed class ClientIpResolverTests
{
    /// <summary>
    /// Verifies spoofed forwarding headers are ignored from untrusted peers.
    /// </summary>
    [TestMethod]
    public void GetClientIp_UntrustedPeerWithForwardedFor_UsesRemoteIp()
    {
        // Arrange
        var resolver = new ClientIpResolver(Options.Create(new SiteChatOptions()));
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        context.Request.Headers["X-Forwarded-For"] = "1.2.3.4";

        // Act
        var ip = resolver.GetClientIp(context);

        // Assert
        ip.Should().Be("203.0.113.10");
    }

    /// <summary>
    /// Verifies forwarding headers are honored for configured trusted proxies.
    /// </summary>
    [TestMethod]
    public void GetClientIp_TrustedPeerWithForwardedFor_UsesForwardedClient()
    {
        // Arrange
        var options = new SiteChatOptions();
        options.Security.TrustedProxyIps.Add("10.0.0.5");
        var resolver = new ClientIpResolver(Options.Create(options));
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.42, 10.0.0.5";

        // Act
        var ip = resolver.GetClientIp(context);

        // Assert
        ip.Should().Be("198.51.100.42");
    }
}
