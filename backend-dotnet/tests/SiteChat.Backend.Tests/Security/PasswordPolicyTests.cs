using FluentAssertions;
using Microsoft.Extensions.Options;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Security;

namespace SiteChat.Backend.Tests.Security;

/// <summary>
/// Tests password policy enforcement.
/// </summary>
[TestClass]
public sealed class PasswordPolicyTests
{
    private readonly PasswordPolicy _policy = new(Options.Create(new SiteChatOptions()));

    /// <summary>
    /// Validates that weak passwords fail complexity checks.
    /// </summary>
    [TestMethod]
    [DataRow("lowercase1!", "uppercase")]
    [DataRow("UPPERCASE1!", "lowercase")]
    [DataRow("NoDigits!!", "digit")]
    [DataRow("NoSpecial1", "special")]
    [DataRow("Aa1!", "at least 8")]
    public void Validate_WeakPassword_ReturnsExpectedError(string password, string expectedErrorFragment)
    {
        // Act
        var result = _policy.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain(expectedErrorFragment);
    }

    /// <summary>
    /// Validates that a complex password passes.
    /// </summary>
    [TestMethod]
    public void Validate_StrongPassword_ReturnsValid()
    {
        // Act
        var result = _policy.Validate("Str0ng!Passw0rd");

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeEmpty();
    }
}
