using System.Security.Claims;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Api.Middleware;

/// <summary>
/// Blocks administrative API usage until an auto-created administrator has changed the initial password.
/// </summary>
public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));

    /// <summary>
    /// Enforces the mandatory password-change gate for admin users.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="repository">The user repository.</param>
    /// <returns>A task that completes when request processing finishes.</returns>
    public async Task InvokeAsync(HttpContext context, IMongoSiteChatRepository repository)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(repository);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = string.IsNullOrWhiteSpace(userId)
                ? null
                : await repository.GetUserByIdAsync(userId, context.RequestAborted).ConfigureAwait(false);

            if (user?.Role == UserRoles.Admin && user.MustChangePassword)
            {
                var isAllowedResetPath = context.Request.Path.Value?.TrimEnd('/') == "/api/auth/me"
                    && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method));

                if (!isAllowedResetPath)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        detail = new
                        {
                            code = "must_change_password",
                            message = "You must set a new password before using the dashboard."
                        }
                    }, context.RequestAborted).ConfigureAwait(false);
                    return;
                }
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}
