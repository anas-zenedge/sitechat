using SiteChat.Backend.Api.Configuration;
using SiteChat.Backend.Api.Models;

namespace SiteChat.Backend.Api.Services;

/// <summary>
/// Evaluates role-scoped site access rules.
/// </summary>
public interface ISiteAccessService
{
    /// <summary>Checks whether a user can view a site.</summary>
    bool CanViewSite(MongoUser user, MongoSite site);
    /// <summary>Checks whether a user can manage a site.</summary>
    bool CanManageSite(MongoUser user, MongoSite site);
}

/// <summary>
/// Implements the same admin, site-owner, and support-agent site scoping used by the Python backend.
/// </summary>
public sealed class SiteAccessService : ISiteAccessService
{
    /// <inheritdoc />
    public bool CanViewSite(MongoUser user, MongoSite site)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(site);

        return user.Role == UserRoles.Admin
            || site.UserId == MongoIdentifiers.GetPublicId(user)
            || (user.Role == UserRoles.Agent && user.AssignedSiteIds.Contains(site.SiteId));
    }

    /// <inheritdoc />
    public bool CanManageSite(MongoUser user, MongoSite site)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(site);

        return user.Role == UserRoles.Admin || site.UserId == MongoIdentifiers.GetPublicId(user);
    }
}
