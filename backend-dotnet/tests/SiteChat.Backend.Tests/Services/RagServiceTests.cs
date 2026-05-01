using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MongoDB.Bson;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Tests.Services;

/// <summary>
/// Tests retrieval-backed chat generation.
/// </summary>
[TestClass]
public sealed class RagServiceTests
{
    /// <summary>
    /// Verifies the RAG service retrieves embedded pages and includes them in the provider prompt.
    /// </summary>
    [TestMethod]
    public async Task ChatAsync_WithEmbeddedPages_ReturnsProviderAnswerAndSources()
    {
        // Arrange
        var conversationRepository = new Mock<IConversationRepository>(MockBehavior.Strict);
        var siteRepository = new Mock<ISiteRepository>(MockBehavior.Strict);
        var pageRepository = new Mock<IPageRepository>(MockBehavior.Strict);
        var openRouterClient = new Mock<IOpenRouterClient>(MockBehavior.Strict);
        var options = Options.Create(new SiteChatOptions
        {
            Rag = new RagOptions
            {
                RetrievalK = 1,
                Temperature = 0.7,
                MaxTokens = 1000
            }
        });
        var service = new RagService(conversationRepository.Object, siteRepository.Object, pageRepository.Object, openRouterClient.Object, options, NullLogger<RagService>.Instance);
        var request = new ChatRequest("Tell me about pricing", "session-1", SiteId: "site-1");
        ChatCompletionRequest? capturedCompletionRequest = null;
        IReadOnlyList<SourceDocument>? savedAssistantSources = null;

        conversationRepository.Setup(repo => repo.SaveMessageAsync("session-1", "user", "Tell me about pricing", "site-1", null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        siteRepository.Setup(repo => repo.GetSiteAsync("site-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MongoSite
            {
                SiteId = "site-1",
                Config = BsonDocument.Parse("""
                    {
                      "behavior": {
                        "system_prompt": "Answer only with grounded site context.",
                        "temperature": 0.15,
                        "max_tokens": 333,
                        "show_sources": true
                      }
                    }
                    """)
            });
        openRouterClient.Setup(client => client.CreateEmbeddingAsync(
                It.Is<EmbeddingRequest>(item => item.Input == "Tell me about pricing" && item.InputType == "search_query" && item.UserId == "session-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([1d, 0d]);
        pageRepository.Setup(repo => repo.GetPagesForRetrievalAsync("site-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new IndexedPage("https://example.com/pricing", "Pricing", "Pricing starts at $29 per month.", 1, "site-1", DateTime.UtcNow, [1d, 0d]),
                new IndexedPage("https://example.com/about", "About", "About our company.", 1, "site-1", DateTime.UtcNow, [0d, 1d])
            ]);
        openRouterClient.Setup(client => client.CompleteAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ChatCompletionRequest, CancellationToken>((chatRequest, _) => capturedCompletionRequest = chatRequest)
            .ReturnsAsync(new ChatCompletionResult("Pricing starts at $29 per month.", 51));
        conversationRepository.Setup(repo => repo.SaveMessageAsync(
                "session-1",
                "assistant",
                "Pricing starts at $29 per month.",
                "site-1",
                It.IsAny<IReadOnlyList<SourceDocument>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string?, IReadOnlyList<SourceDocument>?, CancellationToken>((_, _, _, _, sources, _) => savedAssistantSources = sources)
            .Returns(Task.CompletedTask);

        // Act
        var response = await service.ChatAsync(request, CancellationToken.None);

        // Assert
        response.Answer.Should().Be("Pricing starts at $29 per month.");
        response.TokensUsed.Should().Be(51);
        response.Sources.Should().ContainSingle();
        response.Sources[0].Title.Should().Be("Pricing");
        capturedCompletionRequest.Should().NotBeNull();
        capturedCompletionRequest!.SystemPrompt.Should().Contain("Answer only with grounded site context.");
        capturedCompletionRequest.UserPrompt.Should().Contain("Source 1");
        capturedCompletionRequest.UserPrompt.Should().Contain("Pricing");
        capturedCompletionRequest.Temperature.Should().Be(0.15);
        capturedCompletionRequest.MaxTokens.Should().Be(333);
        savedAssistantSources.Should().BeEquivalentTo(response.Sources);
        conversationRepository.VerifyAll();
        siteRepository.VerifyAll();
        pageRepository.VerifyAll();
        openRouterClient.VerifyAll();
    }

    /// <summary>
    /// Verifies chat failures return the existing safe fallback response.
    /// </summary>
    [TestMethod]
    public async Task ChatAsync_WhenChatCompletionFails_ReturnsFallbackAnswer()
    {
        // Arrange
        var conversationRepository = new Mock<IConversationRepository>(MockBehavior.Strict);
        var siteRepository = new Mock<ISiteRepository>(MockBehavior.Strict);
        var pageRepository = new Mock<IPageRepository>(MockBehavior.Strict);
        var openRouterClient = new Mock<IOpenRouterClient>(MockBehavior.Strict);
        var options = Options.Create(new SiteChatOptions());
        var service = new RagService(conversationRepository.Object, siteRepository.Object, pageRepository.Object, openRouterClient.Object, options, NullLogger<RagService>.Instance);
        var request = new ChatRequest("Where can I contact support?", "session-2");

        conversationRepository.Setup(repo => repo.SaveMessageAsync("session-2", "user", "Where can I contact support?", null, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        openRouterClient.Setup(client => client.CreateEmbeddingAsync(
                It.IsAny<EmbeddingRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.5d, 0.5d]);
        pageRepository.Setup(repo => repo.GetPagesForRetrievalAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<IndexedPage>());
        openRouterClient.Setup(client => client.CompleteAsync(It.IsAny<ChatCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("OpenRouter API key is not configured."));
        conversationRepository.Setup(repo => repo.SaveMessageAsync(
                "session-2",
                "assistant",
                It.Is<string>(message => message.StartsWith("I don't have enough indexed context", StringComparison.Ordinal)),
                null,
                It.IsAny<IReadOnlyList<SourceDocument>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await service.ChatAsync(request, CancellationToken.None);

        // Assert
        response.Answer.Should().StartWith("I don't have enough indexed context");
        response.Sources.Should().BeEmpty();
        conversationRepository.VerifyAll();
        pageRepository.VerifyAll();
        openRouterClient.VerifyAll();
    }
}
