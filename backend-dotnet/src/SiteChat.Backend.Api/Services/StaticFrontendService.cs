using System.Security.Cryptography;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Options;
using SiteChat.Backend.Api.Configuration;

namespace SiteChat.Backend.Api.Services;

/// <summary>
/// Serves the existing frontend HTML files from the repository root.
/// </summary>
public interface IStaticFrontendService
{
    /// <summary>Gets an HTML file response or a JSON service-info fallback.</summary>
    Task<IResult> GetHtmlOrInfoAsync(string fileName, bool includeWidgetSri = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements static frontend placeholder substitution without exposing admin credentials.
/// </summary>
public sealed class StaticFrontendService(IWebHostEnvironment environment, IOptions<SiteChatOptions> options) : IStaticFrontendService
{
    private readonly IWebHostEnvironment _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    private readonly SiteChatOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async Task<IResult> GetHtmlOrInfoAsync(string fileName, bool includeWidgetSri = false, CancellationToken cancellationToken = default)
    {
        var frontendRoot = GetFrontendRoot();
        var filePath = Path.Combine(frontendRoot, fileName);
        if (!File.Exists(filePath))
        {
            return Results.Ok(new
            {
                name = _options.AppName,
                version = "1.0.0",
                status = "running",
                docs = _options.IsProduction ? null : "/api/docs",
                frontend_path = frontendRoot,
                exists = Directory.Exists(frontendRoot)
            });
        }

        var html = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        html = html.Replace("__APP_NAME__", _options.AppName, StringComparison.Ordinal);
        html = html.Replace("__SITE_URL__", _options.SiteUrl, StringComparison.Ordinal);
        html = html.Replace("__ADMIN_EMAIL__", string.Empty, StringComparison.Ordinal);

        if (includeWidgetSri)
        {
            html = html.Replace("__WIDGET_SRI__", GetWidgetSri(frontendRoot) ?? string.Empty, StringComparison.Ordinal);
        }

        return Results.Content(html, "text/html; charset=utf-8");
    }

    private string GetFrontendRoot() =>
        string.IsNullOrWhiteSpace(_options.StaticFiles.FrontendRoot)
            ? Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "..", "..", "frontend"))
            : _options.StaticFiles.FrontendRoot;

    private static string? GetWidgetSri(string frontendRoot)
    {
        var path = Path.Combine(frontendRoot, "widget", "chatbot.js");
        if (!File.Exists(path))
        {
            return null;
        }

        var hash = SHA384.HashData(File.ReadAllBytes(path));
        return "sha384-" + Convert.ToBase64String(hash);
    }
}
