using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Security;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Tests.Services;

/// <summary>
/// Tests authentication service role management behavior.
/// </summary>
[TestClass]
public sealed class AuthServiceTests
{
    /// <summary>
    /// Verifies unsupported roles are rejected before persistence.
    /// </summary>
    [TestMethod]
    public async Task UpdateUserRoleAsync_InvalidRole_ThrowsInvalidOperationException()
    {
        // Arrange
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        var siteRepository = new Mock<ISiteRepository>(MockBehavior.Strict);
        var passwordPolicy = new Mock<IPasswordPolicy>(MockBehavior.Strict);
        var tokenService = new Mock<ITokenService>(MockBehavior.Strict);
        var service = new AuthService(repository.Object, siteRepository.Object, passwordPolicy.Object, tokenService.Object, Options.Create(new SiteChatOptions()));

        // Act
        var act = async () => await service.UpdateUserRoleAsync("user-1", "super-admin", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Role must be one of: admin, user, agent");
    }

    /// <summary>
    /// Verifies valid roles are normalized before they are saved.
    /// </summary>
    [TestMethod]
    public async Task UpdateUserRoleAsync_ValidRole_DelegatesToRepository()
    {
        // Arrange
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        var siteRepository = new Mock<ISiteRepository>(MockBehavior.Strict);
        var passwordPolicy = new Mock<IPasswordPolicy>(MockBehavior.Strict);
        var tokenService = new Mock<ITokenService>(MockBehavior.Strict);
        var service = new AuthService(repository.Object, siteRepository.Object, passwordPolicy.Object, tokenService.Object, Options.Create(new SiteChatOptions()));

        repository.Setup(item => item.UpdateUserRoleAsync("user-1", "agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var updated = await service.UpdateUserRoleAsync("user-1", " Agent ", CancellationToken.None);

        // Assert
        updated.Should().BeTrue();
        repository.VerifyAll();
    }
}
