using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UTTTGO.Api.Modules.Notifications.Infrastructure;

public sealed class DeviceTokenDocument
{
    [BsonId] public ObjectId Id { get; set; }
    public string UserId { get; set; } = "";
    public string ProtectedToken { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public string InstallationId { get; set; } = "";
    public string Platform { get; set; } = "";
    public string? AppVersion { get; set; }
    public bool Active { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
