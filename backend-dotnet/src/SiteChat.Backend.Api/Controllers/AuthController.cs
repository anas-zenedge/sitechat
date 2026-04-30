using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Services;

namespace SiteChat.Backend.Api.Controllers;

/// <summary>
/// Provides authentication and account-management endpoints.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, IMongoSiteChatRepository repository) : SiteChatControllerBase
{
    private readonly IAuthService _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly IMongoSiteChatRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <summary>Authenticates a user.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> LoginAsync([FromBody] UserLogin request, CancellationToken cancellationToken)
    {
        var token = await _authService.LoginAsync(request, cancellationToken).ConfigureAwait(false);
        return token is null ? Unauthorized(new { detail = "Invalid email or password" }) : Ok(token);
    }

    /// <summary>Gets the current user profile.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> GetMeAsync(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        return UnauthorizedIfMissing(user) ?? Ok(AuthService.ToResponse(user!));
    }

    /// <summary>Updates the current user profile.</summary>
    [HttpPatch("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> UpdateMeAsync([FromBody] ProfileUpdate request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(user) is { } missing)
        {
            return missing;
        }

        try
        {
            var updated = await _authService.UpdateProfileAsync(user!, request, cancellationToken).ConfigureAwait(false);
            return updated is null ? NotFound(new { detail = "User not found" }) : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>Creates a site-owner account.</summary>
    [HttpPost("users")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public async Task<ActionResult<UserResponse>> CreateUserAsync([FromBody] AdminUserCreate request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _authService.CreateUserAsync(request, cancellationToken).ConfigureAwait(false);
            return user is null ? BadRequest(new { detail = "Email already registered" }) : Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>Updates a site-owner account.</summary>
    [HttpPatch("users/{userId}")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public async Task<ActionResult<UserResponse>> UpdateUserAsync(string userId, [FromBody] SiteOwnerUpdate request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _authService.UpdateSiteOwnerAsync(userId, request, cancellationToken).ConfigureAwait(false);
            return user is null ? NotFound(new { detail = "User not found or not a site owner" }) : Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>Lists users.</summary>
    [HttpGet("users")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> ListUsersAsync(CancellationToken cancellationToken) =>
        Ok(await _authService.ListUsersAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Updates a user role.</summary>
    [HttpPut("users/{userId}/role")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public async Task<ActionResult<ApiMessageResponse>> UpdateRoleAsync(string userId, [FromQuery] string role, CancellationToken cancellationToken)
    {
        try
        {
            var ok = await _authService.UpdateUserRoleAsync(userId, role, cancellationToken).ConfigureAwait(false);
            return ok ? Ok(new ApiMessageResponse("Role updated successfully")) : NotFound(new { detail = "User not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>Deletes a user.</summary>
    [HttpDelete("users/{userId}")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public async Task<ActionResult<ApiMessageResponse>> DeleteUserAsync(string userId, CancellationToken cancellationToken)
    {
        var ok = await _authService.DeleteUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new ApiMessageResponse("User deleted successfully. Sites and agents transferred to admin.")) : NotFound(new { detail = "User not found" });
    }

    /// <summary>Creates a support agent.</summary>
    [HttpPost("agents")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrUser)]
    public async Task<ActionResult<UserResponse>> CreateAgentAsync([FromBody] AgentCreate request, CancellationToken cancellationToken)
    {
        var caller = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(caller) is { } missing)
        {
            return missing;
        }

        try
        {
            var agent = await _authService.CreateAgentAsync(caller!, request, cancellationToken).ConfigureAwait(false);
            return agent is null ? BadRequest(new { detail = "Email already registered" }) : Ok(agent);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>Lists support agents.</summary>
    [HttpGet("agents")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrUser)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> ListAgentsAsync(CancellationToken cancellationToken)
    {
        var caller = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        return UnauthorizedIfMissing(caller) ?? Ok(await _authService.ListAgentsAsync(caller!, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Updates a support agent.</summary>
    [HttpPatch("agents/{agentId}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrUser)]
    public async Task<ActionResult<UserResponse>> UpdateAgentAsync(string agentId, [FromBody] AgentUpdate request, CancellationToken cancellationToken)
    {
        var caller = await GetCurrentUserAsync(_repository, cancellationToken).ConfigureAwait(false);
        if (UnauthorizedIfMissing(caller) is { } missing)
        {
            return missing;
        }

        var agent = await _authService.UpdateAgentAsync(caller!, agentId, request, cancellationToken).ConfigureAwait(false);
        return agent is null ? NotFound(new { detail = "Agent not found" }) : Ok(agent);
    }

    /// <summary>Deletes a support agent.</summary>
    [HttpDelete("agents/{agentId}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrUser)]
    public async Task<ActionResult<ApiMessageResponse>> DeleteAgentAsync(string agentId, CancellationToken cancellationToken)
    {
        var ok = await _authService.DeleteUserAsync(agentId, cancellationToken).ConfigureAwait(false);
        return ok ? Ok(new ApiMessageResponse("Agent deleted")) : NotFound(new { detail = "Agent not found" });
    }
}
