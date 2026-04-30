using System.Text.RegularExpressions;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Security;

namespace SiteChat.Backend.Api.Services;

/// <summary>
/// Handles crawl job creation and safe page retrieval.
/// </summary>
public interface ICrawlService
{
    /// <summary>Starts a crawl operation.</summary>
    Task<CrawlResponse> StartCrawlAsync(CrawlRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Implements safe single-page crawl ingestion with SSRF protection.
/// </summary>
public sealed class CrawlService(
    ICrawlerUrlValidator urlValidator,
    IMongoSiteChatRepository repository,
    IAiProviderClient aiProviderClient,
    IHttpClientFactory httpClientFactory,
    ILogger<CrawlService> logger) : ICrawlService
{
    private readonly ICrawlerUrlValidator _urlValidator = urlValidator ?? throw new ArgumentNullException(nameof(urlValidator));
    private readonly IMongoSiteChatRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IAiProviderClient _aiProviderClient = aiProviderClient ?? throw new ArgumentNullException(nameof(aiProviderClient));
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly ILogger<CrawlService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<CrawlResponse> StartCrawlAsync(CrawlRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await _urlValidator.ValidateAsync(request.Url, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid || validation.Uri is null)
        {
            throw new InvalidOperationException(validation.ErrorMessage);
        }

        var job = await _repository.CreateCrawlJobAsync(validation.Uri.ToString(), cancellationToken).ConfigureAwait(false);
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var html = await client.GetStringAsync(validation.Uri, cancellationToken).ConfigureAwait(false);
            var title = ExtractTitle(html) ?? validation.Uri.Host;
            var text = StripHtml(html);
            IReadOnlyList<double>? embedding = null;
            try
            {
                embedding = await _aiProviderClient.CreateEmbeddingAsync(
                    new EmbeddingRequest(PrepareEmbeddingInput(text), "search_document"),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "OpenRouter embeddings failed during crawl for {Url}", validation.Uri);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "OpenRouter embeddings were not available during crawl for {Url}", validation.Uri);
            }

            await _repository.SavePageAsync(validation.Uri.ToString(), title, text, Math.Max(1, text.Length / 1000), null, embedding, cancellationToken).ConfigureAwait(false);
            await _repository.UpdateCrawlJobAsync(job.ObjectId.ToString(), "completed", 1, 1, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Crawl failed for {Url}", validation.Uri);
            await _repository.UpdateCrawlJobAsync(job.ObjectId.ToString(), "failed", 0, 0, "Crawl failed", cancellationToken).ConfigureAwait(false);
        }

        return new CrawlResponse(job.ObjectId.ToString(), "Crawl job started", "running");
    }

    private static string? ExtractTitle(string html)
    {
        var match = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim()) : null;
    }

    private static string StripHtml(string html)
    {
        var withoutScripts = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var withoutTags = Regex.Replace(withoutScripts, "<[^>]+>", " ");
        return Regex.Replace(System.Net.WebUtility.HtmlDecode(withoutTags), @"\s+", " ").Trim();
    }

    private static string PrepareEmbeddingInput(string text) =>
        text.Length <= 8000 ? text : text[..8000];
}
