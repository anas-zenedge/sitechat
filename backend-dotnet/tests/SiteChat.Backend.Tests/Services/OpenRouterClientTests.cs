using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Tests.Services;

/// <summary>
/// Tests OpenRouter chat and embeddings client behavior.
/// </summary>
[TestClass]
public sealed class OpenRouterClientTests
{
    /// <summary>
    /// Verifies chat completions use the expected endpoint, headers, and response parsing.
    /// </summary>
    [TestMethod]
    public async Task CompleteAsync_WithSuccessfulResponse_ReturnsChatCompletion()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            capturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "choices": [
                        {
                          "message": {
                            "content": "OpenRouter says hello."
                          }
                        }
                      ],
                      "usage": {
                        "total_tokens": 27
                      }
                    }
                    """)
            };
        });
        var client = CreateClient(handler);

        // Act
        var response = await client.CompleteAsync(new ChatCompletionRequest("System guidance", "User prompt", 0.25, 256, "user-123"), CancellationToken.None);

        // Assert
        response.Content.Should().Be("OpenRouter says hello.");
        response.TotalTokens.Should().Be(27);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.AbsoluteUri.Should().Be("https://openrouter.ai/api/v1/chat/completions");
        capturedRequest.Headers.Authorization.Should().BeEquivalentTo(new AuthenticationHeaderValue("Bearer", "test-openrouter-key"));
        capturedRequest.Headers.GetValues("HTTP-Referer").Single().Should().Be("https://sitechat.local");
        capturedRequest.Headers.GetValues("X-OpenRouter-Title").Single().Should().Be("SiteChat Tests");
        using var json = JsonDocument.Parse(capturedBody!);
        json.RootElement.GetProperty("model").GetString().Should().Be("openai/gpt-4o-mini");
        json.RootElement.GetProperty("messages")[0].GetProperty("role").GetString().Should().Be("system");
        json.RootElement.GetProperty("messages")[1].GetProperty("content").GetString().Should().Be("User prompt");
    }

    /// <summary>
    /// Verifies embeddings use the embeddings endpoint and return the parsed vector.
    /// </summary>
    [TestMethod]
    public async Task CreateEmbeddingAsync_WithSuccessfulResponse_ReturnsVector()
    {
        // Arrange
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "data": [
                        {
                          "embedding": [0.1, 0.2, 0.3]
                        }
                      ]
                    }
                    """)
            };
        });
        var client = CreateClient(handler);

        // Act
        var embedding = await client.CreateEmbeddingAsync(new EmbeddingRequest("embed this", "search_document", "user-456"), CancellationToken.None);

        // Assert
        embedding.Should().Equal(0.1, 0.2, 0.3);
        using var json = JsonDocument.Parse(capturedBody!);
        json.RootElement.GetProperty("model").GetString().Should().Be("openai/text-embedding-3-small");
        json.RootElement.GetProperty("input_type").GetString().Should().Be("search_document");
    }

    private static OpenRouterClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };

        var options = Options.Create(new SiteChatOptions
        {
            AppName = "SiteChat Tests",
            SiteUrl = "https://sitechat.local",
            Rag = new RagOptions
            {
                OpenRouterApiKey = "test-openrouter-key",
                LlmModel = "openai/gpt-4o-mini",
                EmbeddingModel = "openai/text-embedding-3-small"
            }
        });

        return new OpenRouterClient(httpClient, options, NullLogger<OpenRouterClient>.Instance);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }
}
