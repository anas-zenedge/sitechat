using FluentAssertions;
using SiteChat.Backend.Api.Security;

namespace SiteChat.Backend.Tests.Security;

/// <summary>
/// Tests SSRF protections for crawler URLs.
/// </summary>
[TestClass]
public sealed class CrawlerUrlValidatorTests
{
    private readonly CrawlerUrlValidator _validator = new();

    /// <summary>
    /// Verifies unsupported URL schemes are rejected.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsync_NonHttpScheme_ReturnsInvalid()
    {
        // Act
        var result = await _validator.ValidateAsync("file:///etc/passwd");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Only http and https");
    }

    /// <summary>
    /// Verifies loopback destinations are rejected.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsync_LoopbackAddress_ReturnsInvalid()
    {
        // Act
        var result = await _validator.ValidateAsync("http://127.0.0.1:8000/admin");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("private");
    }

    /// <summary>
    /// Verifies link-local metadata-service destinations are rejected.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsync_MetadataServiceAddress_ReturnsInvalid()
    {
        // Act
        var result = await _validator.ValidateAsync("http://169.254.169.254/latest/meta-data/");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("private");
    }
}
