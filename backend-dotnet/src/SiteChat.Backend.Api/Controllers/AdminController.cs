using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Api.Controllers;

/// <summary>
/// Provides authenticated administrative operations.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin")]
public sealed class AdminController(IMongoSiteChatRepository repository, IAiProviderClient aiProviderClient, IOptions<SiteChatOptions> options) : ControllerBase
{
    private readonly IMongoSiteChatRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IAiProviderClient _aiProviderClient = aiProviderClient ?? throw new ArgumentNullException(nameof(aiProviderClient));
    private readonly SiteChatOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <summary>Checks health for backend dependencies.</summary>
    [HttpGet("health")]
    public async Task<ActionResult<HealthCheckResponse>> HealthAsync(CancellationToken cancellationToken)
    {
        var mongoHealthy = await _repository.IsHealthyAsync(cancellationToken).ConfigureAwait(false);
        var aiProviderHealthy = await _aiProviderClient.IsHealthyAsync(cancellationToken).ConfigureAwait(false);
        var status = mongoHealthy && aiProviderHealthy ? "healthy" : "degraded";
        return Ok(new HealthCheckResponse(
            status,
            mongoHealthy ? "healthy" : "unhealthy",
            aiProviderHealthy ? "healthy" : "unhealthy",
            aiProviderHealthy ? "healthy" : "unhealthy"));
    }

    /// <summary>Gets aggregate system statistics.</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<SystemStats>> StatsAsync(CancellationToken cancellationToken) =>
        Ok(await _repository.GetSystemStatsAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Gets non-sensitive runtime configuration.</summary>
    [HttpGet("config")]
    public ActionResult<object> Config() => Ok(new
    {
        app_name = _options.AppName,
        ollama_model = _options.Rag.LlmModel,
        embedding_model = _options.Rag.EmbeddingModel,
        llm_provider = _options.Rag.LlmProvider,
        retrieval_k = _options.Rag.RetrievalK,
        rate_limit = $"{_options.RateLimit.Requests}/{_options.RateLimit.WindowSeconds}s"
    });

    /// <summary>Clears application caches.</summary>
    [HttpPost("clear-cache")]
    public ActionResult<object> ClearCache() => Ok(new { success = true, message = "Caches cleared" });

    /// <summary>Clears all platform data.</summary>
    [HttpDelete("clear-all")]
    public async Task<ActionResult<object>> ClearAllAsync(CancellationToken cancellationToken)
    {
        await _repository.ClearOperationalDataAsync(cancellationToken).ConfigureAwait(false);
        return Ok(new { success = true, message = "All operational data cleared" });
    }
}
