using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Orders.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Orders.Infrastructure;

public sealed class MongoOrderAuditWriter(MongoStore store) : IOrderAuditWriter
{
    public async Task WriteAsync(OrderAuditRecord audit, CancellationToken cancellationToken)
    {
        var document = new OrderAuditLogDocument
        {
            Id = ObjectId.GenerateNewId(),
            OrderId = audit.OrderId,
            ActorUserId = audit.ActorUserId,
            ActorRole = audit.ActorRole,
            Action = audit.Action,
            FromStatus = audit.FromStatus,
            ToStatus = audit.ToStatus,
            CorrelationId = audit.CorrelationId,
            OccurredAt = audit.OccurredAt
        };
        var logs = store.Database.GetCollection<OrderAuditLogDocument>("orderAuditLogs");
        await logs.InsertOneAsync(document, cancellationToken: cancellationToken);
    }
}
