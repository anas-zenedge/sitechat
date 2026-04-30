using SiteChat.Backend.Api.Models;

namespace SiteChat.Backend.Api.Services;

internal static class MongoIdentifiers
{
    internal static string GetPublicId(MongoUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return string.IsNullOrWhiteSpace(user.UserId) ? user.ObjectId.ToString() : user.UserId;
    }
}
