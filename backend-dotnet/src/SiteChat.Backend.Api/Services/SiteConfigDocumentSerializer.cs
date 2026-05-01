using System.Text.Json;
using MongoDB.Bson;
using SiteChat.Backend.Api.Models;

namespace SiteChat.Backend.Api.Services;

internal static class SiteConfigDocumentSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    internal static SiteConfig Read(BsonDocument? document)
    {
        if (document is null || document.ElementCount == 0)
        {
            return new SiteConfig();
        }

        return JsonSerializer.Deserialize<SiteConfig>(document.ToJson(), JsonOptions) ?? new SiteConfig();
    }

    internal static BsonDocument Write(SiteConfig config) =>
        BsonDocument.Parse(JsonSerializer.Serialize(config, JsonOptions));
}
