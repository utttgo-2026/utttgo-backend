using UTTTGO.Api.Modules.Admin.Application;
using UTTTGO.Api.Modules.Catalog.Application;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Notifications.Application;
using UTTTGO.Api.Shared.Infrastructure;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace UTTTGO.Api.Modules.Admin.Infrastructure;

public sealed class AdminDeviceTokenStatusRemover(IDeviceTokenStore store) : IDeviceTokenStatusRemover
{
    public Task RemoveForUserAsync(string userId, CancellationToken cancellationToken) =>
        store.RemoveForUserAsync(userId, cancellationToken);
}

public sealed class RedisAdminUserStatusRateLimitGate(RedisRateLimiter limiter) : IAdminUserStatusRateLimitGate
{
    public Task<bool> IsAllowedAsync(string actorId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return limiter.IsAllowedAsync(("admin-user-status", actorId));
    }
}

public sealed class MongoAdminUserStatusAudit(MongoStore store) : IAdminUserStatusAudit
{
    public async Task RecordAsync(AdminUserStatusAudit audit, CancellationToken cancellationToken)
    {
        var document = new AdminUserStatusAuditDocument
        {
            ActorId = audit.ActorId,
            TargetId = audit.TargetId,
            Action = audit.Action,
            PreviousStatus = audit.PreviousStatus,
            NewStatus = audit.NewStatus,
            OccurredAtUtc = audit.OccurredAtUtc,
            CorrelationId = audit.CorrelationId
        };
        await store.Database.GetCollection<AdminUserStatusAuditDocument>("adminUserStatusAuditLogs")
            .InsertOneAsync(document, cancellationToken: cancellationToken);
    }
}

[BsonIgnoreExtraElements]
internal sealed class AdminUserStatusAuditDocument
{
    [BsonId] public ObjectId Id { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime OccurredAtUtc { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
