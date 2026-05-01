using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Api.Controllers;

/// <summary>
/// Provides authenticated crawl operations.
/// </summary>
[ApiController]
[Authorize]
[Route("api/crawl")]
public sealed class CrawlController(
    ICrawlService crawlService,
    ICrawlJobRepository crawlJobRepository,
    IPageRepository pageRepository) : ControllerBase
{
    private readonly ICrawlService _crawlService = crawlService ?? throw new ArgumentNullException(nameof(crawlService));
    private readonly ICrawlJobRepository _crawlJobRepository = crawlJobRepository ?? throw new ArgumentNullException(nameof(crawlJobRepository));
    private readonly IPageRepository _pageRepository = pageRepository ?? throw new ArgumentNullException(nameof(pageRepository));

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
        var job = await _crawlJobRepository.GetCrawlJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        return job is null
            ? NotFound(new { detail = "Crawl job not found" })
            : Ok(new CrawlStatus(job.ObjectId.ToString(), job.Status, job.PagesCrawled, job.PagesIndexed, job.Errors, job.CreatedAt, job.Status == "completed" ? job.UpdatedAt : null));
    }

    /// <summary>Gets the latest crawl job.</summary>
    [HttpGet("latest")]
    public async Task<ActionResult<CrawlStatus>> LatestAsync(CancellationToken cancellationToken)
    {
        var job = await _crawlJobRepository.GetLatestCrawlJobAsync(cancellationToken).ConfigureAwait(false);
        return job is null
            ? NotFound(new { detail = "No crawl jobs found" })
            : Ok(new CrawlStatus(job.ObjectId.ToString(), job.Status, job.PagesCrawled, job.PagesIndexed, job.Errors, job.CreatedAt, job.Status == "completed" ? job.UpdatedAt : null));
    }

    /// <summary>Returns a reindex placeholder response.</summary>
    [HttpPost("reindex")]
    public ActionResult<object> Reindex() => Ok(new { success = true, message = "Reindex requested" });

    /// <summary>Lists indexed pages.</summary>
    [HttpGet("pages")]
    public async Task<ActionResult<IReadOnlyList<IndexedPageSummary>>> PagesAsync(CancellationToken cancellationToken) =>
        Ok(await _pageRepository.ListPagesAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Deletes an indexed page.</summary>
    [HttpDelete("pages/{*url}")]
    public async Task<ActionResult<object>> DeletePageAsync(string url, CancellationToken cancellationToken)
    {
        var deleted = await _pageRepository.DeletePageAsync(url, cancellationToken).ConfigureAwait(false);
        return deleted ? Ok(new { success = true, message = $"Page {url} deleted" }) : NotFound(new { detail = "Page not found" });
    }
}
