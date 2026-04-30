using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Api.Controllers;

/// <summary>
/// Provides authenticated crawl operations.
/// </summary>
[ApiController]
[Authorize]
[Route("api/crawl")]
public sealed class CrawlController(ICrawlService crawlService, IMongoSiteChatRepository repository) : ControllerBase
{
    private readonly ICrawlService _crawlService = crawlService ?? throw new ArgumentNullException(nameof(crawlService));
    private readonly IMongoSiteChatRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <summary>Starts a crawl job.</summary>
    [HttpPost("")]
    public async Task<ActionResult<CrawlResponse>> StartAsync([FromBody] CrawlRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _crawlService.StartCrawlAsync(request, cancellationToken).ConfigureAwait(false));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>Gets crawl job status.</summary>
    [HttpGet("status/{jobId}")]
    public async Task<ActionResult<CrawlStatus>> StatusAsync(string jobId, CancellationToken cancellationToken)
    {
        var job = await _repository.GetCrawlJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        return job is null
            ? NotFound(new { detail = "Crawl job not found" })
            : Ok(new CrawlStatus(job.ObjectId.ToString(), job.Status, job.PagesCrawled, job.PagesIndexed, job.Errors, job.CreatedAt, job.Status == "completed" ? job.UpdatedAt : null));
    }

    /// <summary>Gets the latest crawl job.</summary>
    [HttpGet("latest")]
    public async Task<ActionResult<CrawlStatus>> LatestAsync(CancellationToken cancellationToken)
    {
        var job = await _repository.GetLatestCrawlJobAsync(cancellationToken).ConfigureAwait(false);
        return job is null
            ? NotFound(new { detail = "No crawl jobs found" })
            : Ok(new CrawlStatus(job.ObjectId.ToString(), job.Status, job.PagesCrawled, job.PagesIndexed, job.Errors, job.CreatedAt, job.Status == "completed" ? job.UpdatedAt : null));
    }

    /// <summary>Returns a reindex placeholder response.</summary>
    [HttpPost("reindex")]
    public ActionResult<object> Reindex() => Ok(new { success = true, message = "Reindex requested" });

    /// <summary>Lists indexed pages.</summary>
    [HttpGet("pages")]
    public async Task<ActionResult<IReadOnlyList<object>>> PagesAsync(CancellationToken cancellationToken)
    {
        var pages = await _repository.ListPagesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(pages.Select(page => new
        {
            url = page.GetValue("url", string.Empty).AsString,
            title = page.GetValue("title", string.Empty).AsString,
            chunk_count = page.GetValue("chunk_count", 0).ToInt32(),
            last_crawled = page.GetValue("last_crawled", BsonNull.Value).IsValidDateTime ? page["last_crawled"].ToUniversalTime() : (DateTime?)null,
            status = page.GetValue("status", string.Empty).AsString
        }).ToList());
    }

    /// <summary>Deletes an indexed page.</summary>
    [HttpDelete("pages/{*url}")]
    public async Task<ActionResult<object>> DeletePageAsync(string url, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeletePageAsync(url, cancellationToken).ConfigureAwait(false);
        return deleted ? Ok(new { success = true, message = $"Page {url} deleted" }) : NotFound(new { detail = "Page not found" });
    }
}
