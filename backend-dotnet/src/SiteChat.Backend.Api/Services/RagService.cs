using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Models;

namespace SiteChat.Backend.Api.Services;

/// <summary>
/// Provides chat response generation through RAG and LLM providers.
/// </summary>
public interface IRagService
{
    /// <summary>
    /// Creates a chat response and persists conversation history.
    /// </summary>
    /// <param name="request">The incoming chat request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The generated chat response.</returns>
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Streams a chat response as text chunks.
    /// </summary>
    /// <param name="request">The incoming chat request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An asynchronous sequence of response chunks.</returns>
    IAsyncEnumerable<string> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Represents a chat-completion request sent to the configured AI provider.
/// </summary>
/// <param name="SystemPrompt">The system prompt to apply.</param>
/// <param name="UserPrompt">The composed user prompt.</param>
/// <param name="Temperature">The temperature to use for generation.</param>
/// <param name="MaxTokens">The maximum number of completion tokens to request.</param>
/// <param name="UserId">The optional end-user identifier.</param>
public sealed record ChatCompletionRequest(string SystemPrompt, string UserPrompt, double Temperature, int MaxTokens, string? UserId = null);

/// <summary>
/// Represents the result of a chat-completion request.
/// </summary>
/// <param name="Content">The generated assistant content.</param>
/// <param name="TotalTokens">The total tokens reported by the provider, when available.</param>
public sealed record ChatCompletionResult(string Content, int? TotalTokens);

/// <summary>
/// Represents an embeddings request sent to the configured AI provider.
/// </summary>
/// <param name="Input">The text to embed.</param>
/// <param name="InputType">The optional input type used by the provider.</param>
/// <param name="UserId">The optional end-user identifier.</param>
public sealed record EmbeddingRequest(string Input, string? InputType = null, string? UserId = null);

/// <summary>
/// Provides OpenRouter-backed chat and embeddings operations.
/// </summary>
public interface IOpenRouterClient : IAiProviderClient
{
}

/// <summary>
/// Calls the OpenRouter HTTP API for chat completions and embeddings.
/// </summary>
public sealed class OpenRouterClient(HttpClient httpClient, IOptions<SiteChatOptions> options, ILogger<OpenRouterClient> logger) : IOpenRouterClient
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly SiteChatOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<OpenRouterClient> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Rag.OpenRouterApiKey))
        {
            _logger.LogDebug("OpenRouter health check skipped because no API key is configured");
            return false;
        }

        try
        {
            using var request = CreateRequest(HttpMethod.Get, "models");
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "OpenRouter health check failed");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureApiKeyConfigured();

        var payload = new
        {
            model = _options.Rag.LlmModel,
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = false,
            user = request.UserId
        };

        using var requestMessage = CreateRequest(HttpMethod.Post, "chat/completions", payload);
        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "chat completion", cancellationToken).ConfigureAwait(false);

        var content = await response.Content.ReadFromJsonAsync<OpenRouterChatResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
        var answer = content?.Choices.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException("OpenRouter returned an empty chat completion.");
        }

        return new ChatCompletionResult(answer, content?.Usage?.TotalTokens);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<double>> CreateEmbeddingAsync(EmbeddingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureApiKeyConfigured();

        var payload = new
        {
            model = _options.Rag.EmbeddingModel,
            input = request.Input,
            encoding_format = "float",
            input_type = request.InputType,
            user = request.UserId
        };

        using var requestMessage = CreateRequest(HttpMethod.Post, "embeddings", payload);
        using var response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "embedding", cancellationToken).ConfigureAwait(false);

        var content = await response.Content.ReadFromJsonAsync<OpenRouterEmbeddingResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
        var embedding = content?.Data.FirstOrDefault()?.Embedding;
        if (embedding is not { Count: > 0 })
        {
            throw new InvalidOperationException("OpenRouter returned an empty embedding vector.");
        }

        return embedding;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, object? payload = null)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Rag.OpenRouterApiKey);

        var referer = string.IsNullOrWhiteSpace(_options.Rag.OpenRouterReferer) ? _options.SiteUrl : _options.Rag.OpenRouterReferer;
        if (!string.IsNullOrWhiteSpace(referer))
        {
            request.Headers.TryAddWithoutValidation("HTTP-Referer", referer);
        }

        var title = string.IsNullOrWhiteSpace(_options.Rag.OpenRouterTitle) ? _options.AppName : _options.Rag.OpenRouterTitle;
        if (!string.IsNullOrWhiteSpace(title))
        {
            request.Headers.TryAddWithoutValidation("X-OpenRouter-Title", title);
        }

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        return request;
    }

    private void EnsureApiKeyConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Rag.OpenRouterApiKey))
        {
            throw new InvalidOperationException("OpenRouter API key is not configured.");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException(
            $"OpenRouter {operation} request failed with status {(int)response.StatusCode}: {body}");
    }

    private sealed class OpenRouterChatResponse
    {
        public List<OpenRouterChatChoice> Choices { get; init; } = [];

        public OpenRouterUsage? Usage { get; init; }
    }

    private sealed class OpenRouterChatChoice
    {
        public OpenRouterChatMessage? Message { get; init; }
    }

    private sealed class OpenRouterChatMessage
    {
        public string? Content { get; init; }
    }

    private sealed class OpenRouterUsage
    {
        public int TotalTokens { get; init; }
    }

    private sealed class OpenRouterEmbeddingResponse
    {
        public List<OpenRouterEmbeddingItem> Data { get; init; } = [];
    }

    private sealed class OpenRouterEmbeddingItem
    {
        public List<double> Embedding { get; init; } = [];
    }
}

/// <summary>
/// Implements a safe, extensible RAG service with OpenRouter-backed retrieval behavior.
/// </summary>
public sealed class RagService(
    IConversationRepository conversationRepository,
    ISiteRepository siteRepository,
    IPageRepository pageRepository,
    IAiProviderClient aiProviderClient,
    IOptions<SiteChatOptions> options,
    ILogger<RagService> logger) : IRagService
{
    private const string InsufficientContextResponse = "I don't have enough indexed context to answer that confidently yet. Please add or crawl site content and try again.";

    private readonly IConversationRepository _conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
    private readonly ISiteRepository _siteRepository = siteRepository ?? throw new ArgumentNullException(nameof(siteRepository));
    private readonly IPageRepository _pageRepository = pageRepository ?? throw new ArgumentNullException(nameof(pageRepository));
    private readonly IAiProviderClient _aiProviderClient = aiProviderClient ?? throw new ArgumentNullException(nameof(aiProviderClient));
    private readonly SiteChatOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<RagService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var message = SanitizePrompt(request.Message);
        await _conversationRepository.SaveMessageAsync(request.SessionId, "user", message, request.SiteId, null, cancellationToken).ConfigureAwait(false);

        var site = !string.IsNullOrWhiteSpace(request.SiteId)
            ? await _siteRepository.GetSiteAsync(request.SiteId, cancellationToken).ConfigureAwait(false)
            : null;
        var siteConfig = SiteConfigDocumentSerializer.Read(site?.Config).Normalize();

        IReadOnlyList<SourceDocument> sources = [];
        int? totalTokens = null;
        string answer;

        try
        {
            sources = await RetrieveSourcesAsync(message, request.SiteId, request.UserId ?? request.SessionId, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "OpenRouter embeddings failed for session {SessionId}", request.SessionId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "OpenRouter embeddings were unavailable for session {SessionId}", request.SessionId);
        }

        try
        {
            var completion = await _aiProviderClient.CompleteAsync(
                new ChatCompletionRequest(
                    BuildSystemPrompt(siteConfig),
                    BuildUserPrompt(message, sources),
                    siteConfig.Behavior?.Temperature ?? _options.Rag.Temperature,
                    siteConfig.Behavior?.MaxTokens ?? _options.Rag.MaxTokens,
                    request.UserId ?? request.SessionId),
                cancellationToken).ConfigureAwait(false);
            answer = completion.Content;
            totalTokens = completion.TotalTokens;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "OpenRouter chat completion failed for session {SessionId}", request.SessionId);
            answer = InsufficientContextResponse;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "OpenRouter chat completion was unavailable for session {SessionId}", request.SessionId);
            answer = InsufficientContextResponse;
        }

        var response = new ChatResponse(
            answer,
            sources,
            answer.StartsWith("I don't have enough", StringComparison.OrdinalIgnoreCase) ? 0.2 : 0.65,
            [],
            request.SessionId,
            totalTokens);

        await _conversationRepository.SaveMessageAsync(request.SessionId, "assistant", response.Answer, request.SiteId, response.Sources, cancellationToken).ConfigureAwait(false);
        return response;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ChatStreamAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await ChatAsync(request, cancellationToken).ConfigureAwait(false);
        foreach (var token in response.Answer.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return token + " ";
            await Task.Yield();
        }
    }

    private static string SanitizePrompt(string value)
    {
        return NormalizeWhitespace(value, 4000);
    }

    private async Task<IReadOnlyList<SourceDocument>> RetrieveSourcesAsync(
        string message,
        string? siteId,
        string userId,
        CancellationToken cancellationToken)
    {
        var queryEmbedding = await _aiProviderClient.CreateEmbeddingAsync(
            new EmbeddingRequest(message, "search_query", userId),
            cancellationToken).ConfigureAwait(false);
        var candidates = await _pageRepository.GetPagesForRetrievalAsync(siteId, cancellationToken).ConfigureAwait(false);

        return candidates
            .Select(page => new
            {
                Page = page,
                Score = CosineSimilarity(queryEmbedding, page.Embedding)
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .Take(_options.Rag.RetrievalK)
            .Select(match => new SourceDocument(
                match.Page.Url,
                string.IsNullOrWhiteSpace(match.Page.Title) ? match.Page.Url : match.Page.Title,
                NormalizeWhitespace(match.Page.Content, 220),
                Math.Round(match.Score, 4)))
            .ToList();
    }

    private string BuildSystemPrompt(SiteConfig siteConfig) =>
        $"{siteConfig.Behavior?.SystemPrompt ?? "You are a helpful assistant. Answer questions based on the provided context."}\n" +
        "Use the provided site context first. If the context is insufficient, clearly say that you do not have enough indexed context to answer confidently.";

    private static string BuildUserPrompt(string message, IReadOnlyList<SourceDocument> sources)
    {
        var context = sources.Count == 0
            ? "No indexed site context was retrieved."
            : string.Join(
                "\n\n",
                sources.Select((source, index) =>
                    $"Source {index + 1}\nTitle: {source.Title}\nUrl: {source.Url}\nExcerpt: {source.ContentPreview}"));

        return $"Site context:\n{context}\n\nUser question:\n{message}";
    }

    private static double CosineSimilarity(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        if (left.Count == 0 || right.Count == 0 || left.Count != right.Count)
        {
            return 0;
        }

        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;
        for (var index = 0; index < left.Count; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude == 0 || rightMagnitude == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    private static string NormalizeWhitespace(string value, int maxLength)
    {
        var withoutNulls = value.Replace("\0", string.Empty, StringComparison.Ordinal);
        var normalized = Regex.Replace(withoutNulls, @"\s+", " ").Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
