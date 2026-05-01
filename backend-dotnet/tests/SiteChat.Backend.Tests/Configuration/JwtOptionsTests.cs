using FluentAssertions;
using SiteChat.Backend.Api.Configuration;

namespace SiteChat.Backend.Tests.Configuration;

/// <summary>
/// Tests JWT secret safety checks.
/// </summary>
[TestClass]
public sealed class JwtOptionsTests
{
    /// <summary>
    /// Verifies known default secrets are not considered production safe.
    /// </summary>
    [TestMethod]
    public void IsSecure_DefaultSecret_ReturnsFalse()
    {
        // Arrange
        var options = new JwtOptions { Secret = "CHANGE-THIS-SECRET-IN-PRODUCTION" };

        // Act
        var secure = options.IsSecure;

        // Assert
        secure.Should().BeFalse();
    }

    /// <summary>
    /// Verifies long non-default secrets are considered production safe.
    /// </summary>
    [TestMethod]
    public void IsSecure_LongRandomSecret_ReturnsTrue()
    {
        // Arrange
        var options = new JwtOptions { Secret = "a-very-long-random-secret-with-more-than-thirty-two-chars" };

        // Act
        var secure = options.IsSecure;

        // Assert
        secure.Should().BeTrue();
    }
}
