using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Api.Controllers;

/// <summary>
/// Provides common controller helpers for authenticated SiteChat endpoints.
/// </summary>
public abstract class SiteChatControllerBase : ControllerBase
{
    /// <summary>
    /// Loads the current MongoDB user from bearer token claims.
    /// </summary>
    /// <param name="repository">The user repository.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The current user.</returns>
    protected async Task<MongoUser?> GetCurrentUserAsync(IMongoSiteChatRepository repository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(userId)
            ? null
            : await repository.GetUserByIdAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Converts a nullable user into an unauthorized response when missing.
    /// </summary>
    /// <param name="user">The loaded user.</param>
    /// <returns>An unauthorized result or <see langword="null" />.</returns>
    protected ActionResult? UnauthorizedIfMissing(MongoUser? user) => user is null ? Unauthorized(new { detail = "User not found" }) : null;
}
