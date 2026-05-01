using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Models;
using SiteChat.Backend.Api.Security;

namespace SiteChat.Backend.Api.Services;

/// <summary>
/// Provides account, authentication, and support-agent operations.
/// </summary>
public interface IAuthService
{
    /// <summary>Authenticates a user and creates a token response.</summary>
    Task<TokenResponse?> LoginAsync(UserLogin login, CancellationToken cancellationToken);
    /// <summary>Creates a site-owner user.</summary>
    Task<UserResponse?> CreateUserAsync(AdminUserCreate request, CancellationToken cancellationToken);
    /// <summary>Creates a support agent.</summary>
    Task<UserResponse?> CreateAgentAsync(MongoUser caller, AgentCreate request, CancellationToken cancellationToken);
    /// <summary>Updates the current profile.</summary>
    Task<UserResponse?> UpdateProfileAsync(MongoUser user, ProfileUpdate request, CancellationToken cancellationToken);
    /// <summary>Updates a site owner.</summary>
    Task<UserResponse?> UpdateSiteOwnerAsync(string userId, SiteOwnerUpdate request, CancellationToken cancellationToken);
    /// <summary>Updates a support agent.</summary>
    Task<UserResponse?> UpdateAgentAsync(MongoUser caller, string agentId, AgentUpdate request, CancellationToken cancellationToken);
    /// <summary>Lists users.</summary>
    Task<IReadOnlyList<UserResponse>> ListUsersAsync(CancellationToken cancellationToken);
    /// <summary>Lists support agents visible to a caller.</summary>
    Task<IReadOnlyList<UserResponse>> ListAgentsAsync(MongoUser caller, CancellationToken cancellationToken);
    /// <summary>Updates a user's role.</summary>
    Task<bool> UpdateUserRoleAsync(string userId, string role, CancellationToken cancellationToken);
    /// <summary>Deletes a user.</summary>
    Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken);
    /// <summary>Ensures an initial admin account exists when configured.</summary>
    Task EnsureAdminExistsAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Implements SiteChat account and authentication behavior.
/// </summary>
public sealed class AuthService(
    IUserRepository userRepository,
    ISiteRepository siteRepository,
    IPasswordPolicy passwordPolicy,
    ITokenService tokenService,
    Microsoft.Extensions.Options.IOptions<SiteChatOptions> options) : IAuthService
{
    private readonly IUserRepository _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly ISiteRepository _siteRepository = siteRepository ?? throw new ArgumentNullException(nameof(siteRepository));
    private readonly IPasswordPolicy _passwordPolicy = passwordPolicy ?? throw new ArgumentNullException(nameof(passwordPolicy));
    private readonly ITokenService _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
    private readonly SiteChatOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async Task<TokenResponse?> LoginAsync(UserLogin login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        var user = await _userRepository.GetUserByEmailAsync(login.Email, cancellationToken).ConfigureAwait(false);
        if (user is null || !BCrypt.Net.BCrypt.Verify(login.Password, user.PasswordHash))
        {
            return null;
        }

        return new TokenResponse(_tokenService.CreateAccessToken(user), "bearer", ToResponse(user));
    }

    /// <inheritdoc />
    public async Task<UserResponse?> CreateUserAsync(AdminUserCreate request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePasswordOrThrow(request.Password);

        if (await _userRepository.GetUserByEmailAsync(request.Email, cancellationToken).ConfigureAwait(false) is not null)
        {
            return null;
        }

        var created = await _userRepository.CreateUserAsync(new MongoUser
        {
            Email = request.Email,
            Name = SanitizeName(request.Name),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRoles.User,
            MustChangePassword = false
        }, cancellationToken).ConfigureAwait(false);

        return ToResponse(created);
    }

    /// <inheritdoc />
    public async Task<UserResponse?> CreateAgentAsync(MongoUser caller, AgentCreate request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        ValidatePasswordOrThrow(request.Password);

        if (await _userRepository.GetUserByEmailAsync(request.Email, cancellationToken).ConfigureAwait(false) is not null)
        {
            return null;
        }

        var assignedSites = request.AssignedSiteIds?.ToList() ?? [];
        if (!await SitesBelongToCallerAsync(caller, assignedSites, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("One or more sites are invalid or not owned by you");
        }

        var created = await _userRepository.CreateUserAsync(new MongoUser
        {
            Email = request.Email,
            Name = SanitizeName(request.Name),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRoles.Agent,
            OwnerId = MongoIdentifiers.GetPublicId(caller),
            AssignedSiteIds = assignedSites,
            MustChangePassword = false
        }, cancellationToken).ConfigureAwait(false);

        return ToResponse(created);
    }

    /// <inheritdoc />
    public async Task<UserResponse?> UpdateProfileAsync(MongoUser user, ProfileUpdate request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(request);

        string? updatedName = null;
        string? passwordHash = null;
        bool? mustChangePassword = null;
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            updatedName = SanitizeName(request.Name);
        }

        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            ValidatePasswordOrThrow(request.NewPassword);
            if (!user.MustChangePassword)
            {
                if (string.IsNullOrEmpty(request.CurrentPassword))
                {
                    throw new InvalidOperationException("Current password required to set a new password");
                }

                if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                {
                    throw new InvalidOperationException("Current password is incorrect");
                }
            }

            passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            mustChangePassword = false;
        }

        if (updatedName is not null || passwordHash is not null || mustChangePassword.HasValue)
        {
            await _userRepository.UpdateUserAsync(
                MongoIdentifiers.GetPublicId(user),
                new UserUpdate(updatedName, passwordHash, mustChangePassword),
                cancellationToken).ConfigureAwait(false);
        }

        var updated = await _userRepository.GetUserByIdAsync(MongoIdentifiers.GetPublicId(user), cancellationToken).ConfigureAwait(false);
        return updated is null ? null : ToResponse(updated);
    }

    /// <inheritdoc />
    public async Task<UserResponse?> UpdateSiteOwnerAsync(string userId, SiteOwnerUpdate request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetUserByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.Role != UserRoles.User)
        {
            return null;
        }

        string? updatedName = null;
        string? passwordHash = null;
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            updatedName = SanitizeName(request.Name);
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            ValidatePasswordOrThrow(request.Password);
            passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        if (updatedName is null && passwordHash is null)
        {
            return ToResponse(user);
        }

        await _userRepository.UpdateUserAsync(userId, new UserUpdate(updatedName, passwordHash), cancellationToken).ConfigureAwait(false);
        var updated = await _userRepository.GetUserByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return updated is null ? null : ToResponse(updated);
    }

    /// <inheritdoc />
    public async Task<UserResponse?> UpdateAgentAsync(MongoUser caller, string agentId, AgentUpdate request, CancellationToken cancellationToken)
    {
        var agent = await _userRepository.GetUserByIdAsync(agentId, cancellationToken).ConfigureAwait(false);
        if (agent is null || agent.Role != UserRoles.Agent)
        {
            return null;
        }

        if (caller.Role != UserRoles.Admin && agent.OwnerId != MongoIdentifiers.GetPublicId(caller))
        {
            return null;
        }

        string? updatedName = null;
        string? passwordHash = null;
        IReadOnlyList<string>? assignedSiteIds = null;
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            updatedName = SanitizeName(request.Name);
        }

        if (request.AssignedSiteIds is not null)
        {
            if (!await SitesBelongToCallerAsync(caller, request.AssignedSiteIds, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("One or more sites are invalid or not owned by you");
            }

            assignedSiteIds = request.AssignedSiteIds.ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            ValidatePasswordOrThrow(request.Password);
            passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        if (updatedName is not null || passwordHash is not null || assignedSiteIds is not null)
        {
            await _userRepository.UpdateUserAsync(agentId, new UserUpdate(updatedName, passwordHash, AssignedSiteIds: assignedSiteIds), cancellationToken).ConfigureAwait(false);
        }

        var updated = await _userRepository.GetUserByIdAsync(agentId, cancellationToken).ConfigureAwait(false);
        return updated is null ? null : ToResponse(updated);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserResponse>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetAllUsersAsync(cancellationToken).ConfigureAwait(false);
        return users.Select(ToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserResponse>> ListAgentsAsync(MongoUser caller, CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetAllUsersAsync(cancellationToken).ConfigureAwait(false);
        var agents = users.Where(user => user.Role == UserRoles.Agent);
        if (caller.Role != UserRoles.Admin)
        {
            var callerId = MongoIdentifiers.GetPublicId(caller);
            agents = agents.Where(user => user.OwnerId == callerId);
        }

        return agents.Select(ToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> UpdateUserRoleAsync(string userId, string role, CancellationToken cancellationToken)
    {
        var normalizedRole = NormalizeRole(role);
        if (!AllowedRoles.Contains(normalizedRole))
        {
            throw new InvalidOperationException("Role must be one of: admin, user, agent");
        }

        return await _userRepository.UpdateUserRoleAsync(userId, normalizedRole, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken) =>
        _userRepository.DeleteUserAsync(userId, cancellationToken);

    /// <inheritdoc />
    public async Task EnsureAdminExistsAsync(CancellationToken cancellationToken)
    {
        if (await _userRepository.GetUserByRoleAsync(UserRoles.Admin, cancellationToken).ConfigureAwait(false) is not null)
        {
            return;
        }

        var configuredPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        var configuredEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@example.com";
        if (string.IsNullOrWhiteSpace(configuredPassword))
        {
            return;
        }

        ValidatePasswordOrThrow(configuredPassword);
        await _userRepository.CreateUserAsync(new MongoUser
        {
            Email = configuredEmail,
            Name = "Administrator",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(configuredPassword),
            Role = UserRoles.Admin,
            MustChangePassword = true
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Converts a MongoDB user into an API response.
    /// </summary>
    /// <param name="user">The MongoDB user.</param>
    /// <returns>The API response shape.</returns>
    public static UserResponse ToResponse(MongoUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new UserResponse(
            MongoIdentifiers.GetPublicId(user),
            user.Email,
            user.Name,
            user.Role,
            user.CreatedAt,
            user.AssignedSiteIds,
            user.MustChangePassword);
    }

    private async Task<bool> SitesBelongToCallerAsync(MongoUser caller, IReadOnlyList<string> siteIds, CancellationToken cancellationToken)
    {
        if (siteIds.Count == 0 || caller.Role == UserRoles.Admin)
        {
            return true;
        }

        var callerId = MongoIdentifiers.GetPublicId(caller);
        foreach (var siteId in siteIds)
        {
            var site = await _siteRepository.GetSiteAsync(siteId, cancellationToken).ConfigureAwait(false);
            if (site?.UserId != callerId)
            {
                return false;
            }
        }

        return true;
    }

    private void ValidatePasswordOrThrow(string password)
    {
        var result = _passwordPolicy.Validate(password);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(result.ErrorMessage);
        }
    }

    private static string SanitizeName(string name)
    {
        var withoutNulls = name.Replace("\0", string.Empty, StringComparison.Ordinal);
        var normalized = string.Join(' ', withoutNulls.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length < 2)
        {
            throw new InvalidOperationException("Name must be at least 2 characters");
        }

        return normalized[..Math.Min(100, normalized.Length)];
    }

    private static string NormalizeRole(string role) => role.Trim().ToLowerInvariant();

    private static readonly HashSet<string> AllowedRoles =
    [
        UserRoles.Admin,
        UserRoles.User,
        UserRoles.Agent
    ];
}
