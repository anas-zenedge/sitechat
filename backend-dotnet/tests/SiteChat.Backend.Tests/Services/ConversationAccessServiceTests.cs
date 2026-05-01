using FluentAssertions;
using Moq;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Tests.Services;

/// <summary>
/// Tests conversation access rules.
/// </summary>
[TestClass]
public sealed class ConversationAccessServiceTests
{
    /// <summary>
    /// Verifies conversations are hidden when a non-admin user cannot access the backing site.
    /// </summary>
    [TestMethod]
    public async Task GetConversationAsync_InaccessibleSite_ReturnsNull()
    {
        // Arrange
        var conversationRepository = new Mock<IConversationRepository>(MockBehavior.Strict);
        var siteRepository = new Mock<ISiteRepository>(MockBehavior.Strict);
        var systemRepository = new Mock<ISystemRepository>(MockBehavior.Strict);
        var service = new ConversationAccessService(conversationRepository.Object, siteRepository.Object, systemRepository.Object, new SiteAccessService());
        var user = new MongoUser
        {
            UserId = "owner-1",
            Role = "user",
            AssignedSiteIds = []
        };

        conversationRepository.Setup(item => item.GetConversationAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MongoConversation
            {
                SessionId = "session-1",
                SiteId = "site-1"
            });
        siteRepository.Setup(item => item.GetSiteAsync("site-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MongoSite
            {
                SiteId = "site-1",
                UserId = "different-owner"
            });

        // Act
        var conversation = await service.GetConversationAsync(user, "session-1", CancellationToken.None);

        // Assert
        conversation.Should().BeNull();
        conversationRepository.VerifyAll();
        siteRepository.VerifyAll();
    }

    /// <summary>
    /// Verifies list queries use only the current user's accessible sites.
    /// </summary>
    [TestMethod]
    public async Task ListConversationsAsync_SiteOwnerWithoutFilter_UsesOwnedSites()
    {
        // Arrange
        var conversationRepository = new Mock<IConversationRepository>(MockBehavior.Strict);
        var siteRepository = new Mock<ISiteRepository>(MockBehavior.Strict);
        var systemRepository = new Mock<ISystemRepository>(MockBehavior.Strict);
        var service = new ConversationAccessService(conversationRepository.Object, siteRepository.Object, systemRepository.Object, new SiteAccessService());
        var user = new MongoUser
        {
            UserId = "owner-1",
            Role = "user"
        };

        siteRepository.Setup(item => item.ListSitesAsync("owner-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MongoSite { SiteId = "site-1", UserId = "owner-1" },
                new MongoSite { SiteId = "site-2", UserId = "owner-1" }
            ]);
        conversationRepository.Setup(item => item.ListConversationsForSitesAsync(
                It.Is<IReadOnlyList<string>>(siteIds => siteIds.SequenceEqual(new[] { "site-1", "site-2" })),
                null,
                1,
                20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                (IReadOnlyList<MongoConversation>)[new MongoConversation { SessionId = "session-1", SiteId = "site-1" }],
                1L));

        // Act
        var result = await service.ListConversationsAsync(user, null, null, 1, 20, CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle(item => item.SessionId == "session-1");
        siteRepository.VerifyAll();
        conversationRepository.VerifyAll();
    }

    /// <summary>
    /// Verifies bulk deletes remove only conversations visible to the caller.
    /// </summary>
    [TestMethod]
    public async Task DeleteConversationsAsync_MixedVisibility_DeletesOnlyAccessibleConversations()
    {
        // Arrange
        var conversationRepository = new Mock<IConversationRepository>(MockBehavior.Strict);
        var siteRepository = new Mock<ISiteRepository>(MockBehavior.Strict);
        var systemRepository = new Mock<ISystemRepository>(MockBehavior.Strict);
        var service = new ConversationAccessService(conversationRepository.Object, siteRepository.Object, systemRepository.Object, new SiteAccessService());
        var user = new MongoUser
        {
            UserId = "owner-1",
            Role = "user"
        };

        conversationRepository.Setup(item => item.GetConversationAsync("allowed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MongoConversation { SessionId = "allowed", SiteId = "site-1" });
        siteRepository.Setup(item => item.GetSiteAsync("site-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MongoSite { SiteId = "site-1", UserId = "owner-1" });
        conversationRepository.Setup(item => item.GetConversationAsync("blocked", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MongoConversation { SessionId = "blocked", SiteId = "site-2" });
        siteRepository.Setup(item => item.GetSiteAsync("site-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MongoSite { SiteId = "site-2", UserId = "other-owner" });
        conversationRepository.Setup(item => item.DeleteConversationsAsync(
                It.Is<IReadOnlyList<string>>(sessionIds => sessionIds.SequenceEqual(new[] { "allowed" })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var deleted = await service.DeleteConversationsAsync(user, ["allowed", "blocked"], CancellationToken.None);

        // Assert
        deleted.Should().Be(1);
        conversationRepository.VerifyAll();
        siteRepository.VerifyAll();
    }
}
