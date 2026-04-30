using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Controllers;

namespace SiteChat.Backend.Tests.Controllers;

/// <summary>
/// Tests authorization metadata for security-sensitive controllers.
/// </summary>
[TestClass]
public sealed class AuthorizationMetadataTests
{
    /// <summary>
    /// Verifies admin endpoints require the admin policy.
    /// </summary>
    [TestMethod]
    public void AdminController_ClassMetadata_RequiresAdminPolicy()
    {
        // Act
        var attribute = typeof(AdminController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false).Cast<AuthorizeAttribute>().Single();

        // Assert
        attribute.Policy.Should().Be(AuthorizationPolicies.Admin);
    }

    /// <summary>
    /// Verifies crawl endpoints require authentication.
    /// </summary>
    [TestMethod]
    public void CrawlController_ClassMetadata_RequiresAuthentication()
    {
        // Act
        var attribute = typeof(CrawlController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false).Cast<AuthorizeAttribute>().Single();

        // Assert
        attribute.Policy.Should().BeNull();
    }

    /// <summary>
    /// Verifies analytics endpoints require authentication.
    /// </summary>
    [TestMethod]
    public void AnalyticsController_ClassMetadata_RequiresAuthentication()
    {
        // Act
        var attribute = typeof(AnalyticsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false).Cast<AuthorizeAttribute>().Single();

        // Assert
        attribute.Policy.Should().BeNull();
    }
}
