using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SiteChat.Backend.Api.Configuration;

namespace SiteChat.Backend.Api.Services;

/// <summary>
/// Provides chat-completion and embedding operations for the configured AI provider.
/// </summary>
public interface IAiProviderClient
{
    /// <summary>
    /// Checks whether the configured provider endpoint is reachable.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true" /> if the provider is reachable; otherwise, <see langword="false" />.</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sends a chat-completion request to the provider.
    /// </summary>
    /// <param name="request">The chat-completion request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A provider chat-completion result.</returns>
    Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Generates an embedding vector through the provider.
    /// </summary>
    /// <param name="request">The embedding request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An embedding vector.</returns>
    Task<IReadOnlyList<double>> CreateEmbeddingAsync(EmbeddingRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Creates the AI provider client configured for the current application instance.
/// </summary>
public interface IAiProviderClientFactory
{
    /// <summary>
    /// Creates the configured AI provider client.
    /// </summary>
    /// <returns>An AI provider client.</returns>
    IAiProviderClient Create();
}

/// <summary>
/// Contains supported AI provider names.
/// </summary>
internal static class AiProviderNames
{
    internal const string OpenRouter = "openrouter";

    internal static bool IsSupported(string? providerName) =>
        string.Equals(providerName?.Trim(), OpenRouter, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Creates AI provider clients using the configured provider name and the active dependency injection scope.
/// </summary>
public sealed class AiProviderClientFactory(IServiceProvider serviceProvider, IOptions<SiteChatOptions> options) : IAiProviderClientFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly SiteChatOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The configured AI provider is not supported.</exception>
    public IAiProviderClient Create() =>
        NormalizeProviderName(_options.Rag.LlmProvider) switch
        {
            AiProviderNames.OpenRouter => _serviceProvider.GetRequiredService<IOpenRouterClient>(),
            var provider => throw new InvalidOperationException(
                $"Unsupported AI provider '{provider}'. Supported providers: {AiProviderNames.OpenRouter}.")
        };

    private static string NormalizeProviderName(string providerName) =>
        string.IsNullOrWhiteSpace(providerName) ? string.Empty : providerName.Trim().ToLowerInvariant();
}
