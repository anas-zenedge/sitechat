namespace SiteChat.Backend.Api.Models;

/// <summary>
/// Represents a crawled page that is available for embedding-backed retrieval.
/// </summary>
/// <param name="Url">The canonical page URL.</param>
/// <param name="Title">The page title.</param>
/// <param name="Content">The normalized page content.</param>
/// <param name="ChunkCount">The stored chunk count metadata.</param>
/// <param name="SiteId">The owning site identifier, when available.</param>
/// <param name="LastCrawled">The last crawl timestamp.</param>
/// <param name="Embedding">The persisted embedding vector.</param>
public sealed record IndexedPage(
    string Url,
    string Title,
    string Content,
    int ChunkCount,
    string? SiteId,
    DateTime? LastCrawled,
    IReadOnlyList<double> Embedding);
