using FluentAssertions;
using Moq;
using MongoDB.Bson;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Tests.Services;

/// <summary>
/// Tests site provisioning and configuration behavior.
/// </summary>
[TestClass]
public sealed class SiteManagementServiceTests
{
    /// <summary>
    /// Verifies site creation assigns ownership to the caller.
    /// </summary>
    [TestMethod]
    public async Task CreateSiteAsync_WithOwner_AssignsPublicOwnerId()
    {
        // Arrange
        var repository = new Mock<IMongoSiteChatRepository>(MockBehavior.Strict);
        var service = new SiteManagementService(repository.Object);
        var owner = new MongoUser
        {
            UserId = "owner-123",
            Email = "owner@example.com",
            Name = "Owner"
        };

        repository.Setup(item => item.CreateSiteAsync(
                It.Is<MongoSite>(site =>
                    site.UserId == "owner-123"
                    && site.Url == "https://example.com"
                    && site.Name == "Example"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MongoSite site, CancellationToken _) => site);

        // Act
        var site = await service.CreateSiteAsync(owner, new SetupRequest("https://example.com", "Example"), CancellationToken.None);

        // Assert
        site.UserId.Should().Be("owner-123");
        site.Url.Should().Be("https://example.com");
        site.Name.Should().Be("Example");
        repository.VerifyAll();
    }

    /// <summary>
    /// Verifies configuration updates merge with the current stored configuration before persistence.
    /// </summary>
    [TestMethod]
    public async Task UpdateConfigAsync_WithExistingSite_MergesAndPersistsConfiguration()
    {
        // Arrange
        var repository = new Mock<IMongoSiteChatRepository>(MockBehavior.Strict);
        var service = new SiteManagementService(repository.Object);
        var site = new MongoSite
        {
            SiteId = "site-1",
            Config = BsonDocument.Parse("""
                {
                  "appearance": {
                    "chat_title": "Original title"
                  },
                  "behavior": {
                    "system_prompt": "Original prompt",
                    "temperature": 0.2
                  }
                }
                """)
        };
        SiteConfig? savedConfig = null;

        repository.Setup(item => item.GetSiteAsync("site-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(site);
        repository.Setup(item => item.SaveSiteConfigAsync("site-1", It.IsAny<SiteConfig>(), It.IsAny<CancellationToken>()))
            .Callback<string, SiteConfig, CancellationToken>((_, config, _) => savedConfig = config)
            .ReturnsAsync(true);

        // Act
        var updated = await service.UpdateConfigAsync(
            "site-1",
            new SiteConfigUpdate(
                Appearance: new SiteAppearanceConfig(ChatTitle: "Updated title"),
                Behavior: new SiteBehaviorConfig(SystemPrompt: "Updated prompt", Temperature: 0.7, MaxTokens: 900, ShowSources: false)),
            CancellationToken.None);

        // Assert
        updated.Should().NotBeNull();
        updated!.Appearance!.ChatTitle.Should().Be("Updated title");
        updated.Behavior!.SystemPrompt.Should().Be("Updated prompt");
        savedConfig.Should().NotBeNull();
        savedConfig!.Appearance!.ChatTitle.Should().Be("Updated title");
        savedConfig.Behavior!.SystemPrompt.Should().Be("Updated prompt");
        repository.VerifyAll();
    }
}
