using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

[BsonIgnoreExtraElements]
public sealed class UserDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("supabaseUserId")]
    public string SupabaseUserId { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [BsonElement("phone")]
    public string? Phone { get; set; }

    [BsonElement("avatarMediaAssetId")]
    public string? AvatarMediaAssetId { get; set; }

    [BsonElement("roles")]
    public List<string> Roles { get; set; } = [];

    [BsonElement("sellerId")]
    public string? SellerId { get; set; }

    [BsonElement("accountStatus")]
    public string AccountStatus { get; set; } = string.Empty;

    [BsonElement("version")]
    public long Version { get; set; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }
}
