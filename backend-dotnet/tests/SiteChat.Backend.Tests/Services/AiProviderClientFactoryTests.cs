using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Tests.Services;

/// <summary>
/// Tests AI provider client factory behavior.
/// </summary>
[TestClass]
public sealed class AiProviderClientFactoryTests
{
    /// <summary>
    /// Verifies the factory resolves the configured OpenRouter provider from the current scope.
    /// </summary>
    [TestMethod]
    public void Create_OpenRouterConfigured_ReturnsResolvedProvider()
    {
        // Arrange
        var provider = new Mock<IOpenRouterClient>(MockBehavior.Strict);
        var serviceProvider = new Mock<IServiceProvider>(MockBehavior.Strict);
        serviceProvider.Setup(item => item.GetService(typeof(IOpenRouterClient)))
            .Returns(provider.Object);
        var factory = new AiProviderClientFactory(
            serviceProvider.Object,
            Options.Create(new SiteChatOptions
            {
                Rag = new RagOptions
                {
                    LlmProvider = "openrouter"
                }
            }));

        // Act
        var result = factory.Create();

        // Assert
        result.Should().BeSameAs(provider.Object);
        serviceProvider.VerifyAll();
    }

    /// <summary>
    /// Verifies unsupported providers are rejected explicitly.
    /// </summary>
    [TestMethod]
    public void Create_UnsupportedProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>(MockBehavior.Strict);
        var factory = new AiProviderClientFactory(
            serviceProvider.Object,
            Options.Create(new SiteChatOptions
            {
                Rag = new RagOptions
                {
                    LlmProvider = "custom-ai"
                }
            }));

        // Act
        var act = factory.Create;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Unsupported AI provider*");
    }
}
