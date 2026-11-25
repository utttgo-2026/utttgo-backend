using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Notifications.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UTTTGO.Api.Modules.Notifications.Infrastructure;

public sealed class FirebaseAdminNotificationSender(IConfiguration configuration, ILogger<FirebaseAdminNotificationSender> logger) : INotificationSender
{
    private readonly Lazy<FirebaseMessaging?> messaging = new(() => Create(configuration, logger));
    private static FirebaseMessaging? Create(IConfiguration config, ILogger logger)
    {
        try
        {
            var json = config["FIREBASE_CREDENTIALS_JSON"];
            var path = config["FIREBASE_CREDENTIALS_PATH"];
            if (string.IsNullOrWhiteSpace(json) && string.IsNullOrWhiteSpace(path))
            {
                logger.LogWarning("Firebase push sender disabled: credentials are not configured.");
                return null;
            }
            var credential = string.IsNullOrWhiteSpace(json) ? GoogleCredential.FromFile(path!) : GoogleCredential.FromJson(json);
            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions { Credential = credential });
            logger.LogInformation("Firebase push sender active.");
            return FirebaseMessaging.GetMessaging(app);
        }
        catch { logger.LogWarning("Firebase push sender disabled: credentials could not be loaded."); return null; }
    }
    public async Task<NotificationSendResult> SendAsync(NotificationMessage message, string token, CancellationToken ct)
    {
        if (messaging.Value is null) return new(NotificationSendStatus.Disabled);
        try
        {
            await messaging.Value.SendAsync(new Message { Token = token, Notification = new Notification { Title = message.Title, Body = message.Body }, Data = message.Data }, ct);
            return new(NotificationSendStatus.Sent);
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
        { return new(NotificationSendStatus.Unregistered); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(NotificationSendStatus.TransientFailure); }
        catch (Exception) { return new(NotificationSendStatus.TransientFailure); }
    }
}

public sealed class MongoNotificationOutbox(MongoStore store) : INotificationEventPublisher
{
    private IMongoCollection<NotificationOutboxDocument> Collection => store.Database.GetCollection<NotificationOutboxDocument>("notificationOutbox");
    public Task PublishAsync(string eventType, string recipientUserId, string orderId, string status, string correlationId, string idempotencyKey, CancellationToken ct) =>
        Collection.UpdateOneAsync(x => x.IdempotencyKey == idempotencyKey,
            Builders<NotificationOutboxDocument>.Update.SetOnInsert(x => x.Id, ObjectId.GenerateNewId()).SetOnInsert(x => x.EventType, eventType)
                .SetOnInsert(x => x.RecipientUserId, recipientUserId).SetOnInsert(x => x.OrderId, orderId).SetOnInsert(x => x.Status, status)
                .SetOnInsert(x => x.CorrelationId, correlationId).SetOnInsert(x => x.IdempotencyKey, idempotencyKey)
                .SetOnInsert(x => x.ProcessingStatus, "Pending").SetOnInsert(x => x.Attempts, 0)
                .SetOnInsert(x => x.CreatedAt, DateTime.UtcNow), new UpdateOptions { IsUpsert = true }, ct);
}
public sealed class NotificationOutboxDocument
{
    [MongoDB.Bson.Serialization.Attributes.BsonId] public ObjectId Id { get; set; }
    public string EventType { get; set; } = ""; public string RecipientUserId { get; set; } = ""; public string OrderId { get; set; } = "";
    public string Status { get; set; } = ""; public string CorrelationId { get; set; } = ""; public string IdempotencyKey { get; set; } = "";
    public string ProcessingStatus { get; set; } = "Pending";
    public string? WorkerId { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
public sealed class NotificationMongoIndexes(MongoStore store, ILogger<NotificationMongoIndexes> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var devices = store.Database.GetCollection<DeviceTokenDocument>("deviceTokens");
            var deviceKeys = Builders<DeviceTokenDocument>.IndexKeys;
            await devices.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<DeviceTokenDocument>(deviceKeys.Ascending(x => x.UserId).Ascending(x => x.TokenHash),
                    new CreateIndexOptions { Unique = true, Name = "user_tokenHash_unique" }),
                new CreateIndexModel<DeviceTokenDocument>(deviceKeys.Ascending(x => x.UserId).Ascending(x => x.InstallationId),
                    new CreateIndexOptions { Unique = true, Name = "user_installation_unique" }),
                new CreateIndexModel<DeviceTokenDocument>(deviceKeys.Ascending(x => x.UserId).Ascending(x => x.Active),
                    new CreateIndexOptions { Name = "user_active" }),
                new CreateIndexModel<DeviceTokenDocument>(deviceKeys.Ascending(x => x.TokenHash),
                    new CreateIndexOptions<DeviceTokenDocument>
                    {
                        Name = "active_tokenHash_unique",
                        Unique = true,
                        PartialFilterExpression = Builders<DeviceTokenDocument>.Filter.Eq(x => x.Active, true)
                    })
            ], ct);

            var outbox = store.Database.GetCollection<NotificationOutboxDocument>("notificationOutbox");
            await outbox.Indexes.CreateOneAsync(new CreateIndexModel<NotificationOutboxDocument>(
                Builders<NotificationOutboxDocument>.IndexKeys.Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions { Unique = true, Name = "idempotencyKey_unique" }),
                cancellationToken: ct);
        }
        catch { logger.LogWarning("Notification indexes could not be created; API startup will continue."); }
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

public sealed class NotificationOutboxDispatcher(MongoStore store, IDeviceTokenStore tokens,
    INotificationSender sender, ILogger<NotificationOutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private readonly string workerId = Guid.NewGuid().ToString("N");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchAsync(stoppingToken); } catch (Exception) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning("Notification outbox dispatch failed; it will be retried."); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
    private async Task DispatchAsync(CancellationToken ct)
    {
        var collection = store.Database.GetCollection<NotificationOutboxDocument>("notificationOutbox");
        for (var i = 0; i < 50; i++)
        {
            var item = await ClaimAsync(collection, ct);
            if (item is null) break;

            var devices = await tokens.FindActiveAsync(item.RecipientUserId, ct);
            if (devices.Count == 0)
            {
                await MarkSentAsync(collection, item.Id, ct);
                continue;
            }

            var transient = false;
            var disabled = false;
            foreach (var device in devices)
            {
                NotificationSendResult result;
                try
                {
                    var message = new NotificationMessage(item.EventType, item.RecipientUserId,
                        "Actualización de pedido", item.EventType == "order.created" ? "Tienes un nuevo pedido." : item.EventType == "order.ready" ? "Tu pedido está listo para recoger." : "Tu pedido tiene una actualización.",
                        new Dictionary<string, string> { ["eventType"] = item.EventType, ["orderId"] = item.OrderId, ["status"] = item.Status }, item.CorrelationId, item.IdempotencyKey);
                    result = await sender.SendAsync(message, tokens.Unprotect(device.ProtectedToken), ct);
                }
                catch { result = new(NotificationSendStatus.TransientFailure); }
                if (result.Status == NotificationSendStatus.Unregistered)
                    await tokens.DeactivateAsync(device.Id, ct);
                else if (result.Status == NotificationSendStatus.TransientFailure)
                    transient = true;
                else if (result.Status == NotificationSendStatus.Disabled)
                    disabled = true;
            }

            if (!transient || disabled)
            {
                await MarkSentAsync(collection, item.Id, ct);
                continue;
            }

            var delay = item.Attempts switch { 1 => TimeSpan.FromSeconds(1), 2 => TimeSpan.FromSeconds(5), _ => TimeSpan.FromSeconds(30) };
            await ScheduleRetryAsync(collection, item, delay, ct);
        }
    }

    private async Task<NotificationOutboxDocument?> ClaimAsync(
        IMongoCollection<NotificationOutboxDocument> collection, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<NotificationOutboxDocument>.Filter.And(
            Builders<NotificationOutboxDocument>.Filter.Lt(x => x.Attempts, 3),
            Builders<NotificationOutboxDocument>.Filter.Or(
                Builders<NotificationOutboxDocument>.Filter.Eq(x => x.ProcessingStatus, "Pending"),
                Builders<NotificationOutboxDocument>.Filter.Exists(x => x.ProcessingStatus, false),
                Builders<NotificationOutboxDocument>.Filter.And(
                    Builders<NotificationOutboxDocument>.Filter.Eq(x => x.ProcessingStatus, "Processing"),
                    Builders<NotificationOutboxDocument>.Filter.Lte(x => x.LeaseUntil, now))),
            Builders<NotificationOutboxDocument>.Filter.Or(
                Builders<NotificationOutboxDocument>.Filter.Eq(x => x.NextAttemptAt, null),
                Builders<NotificationOutboxDocument>.Filter.Lte(x => x.NextAttemptAt, now)));
        var update = Builders<NotificationOutboxDocument>.Update.Combine(
            Builders<NotificationOutboxDocument>.Update.Set(x => x.ProcessingStatus, "Processing"),
            Builders<NotificationOutboxDocument>.Update.Set(x => x.WorkerId, workerId),
            Builders<NotificationOutboxDocument>.Update.Set(x => x.LeaseUntil, now + LeaseDuration),
            Builders<NotificationOutboxDocument>.Update.Inc(x => x.Attempts, 1));
        return await collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<NotificationOutboxDocument>
            {
                ReturnDocument = ReturnDocument.After,
                Sort = Builders<NotificationOutboxDocument>.Sort.Ascending(x => x.CreatedAt)
            }, ct);
    }

    private Task MarkSentAsync(IMongoCollection<NotificationOutboxDocument> collection, ObjectId id,
        CancellationToken ct) => collection.UpdateOneAsync(
        x => x.Id == id && x.ProcessingStatus == "Processing" && x.WorkerId == workerId,
        Builders<NotificationOutboxDocument>.Update.Combine(
            Builders<NotificationOutboxDocument>.Update.Set(x => x.ProcessingStatus, "Sent"),
            Builders<NotificationOutboxDocument>.Update.Set(x => x.SentAt, DateTime.UtcNow),
            Builders<NotificationOutboxDocument>.Update.Set<string?>(x => x.WorkerId, null),
            Builders<NotificationOutboxDocument>.Update.Set<DateTime?>(x => x.LeaseUntil, null)), cancellationToken: ct);

    private Task ScheduleRetryAsync(IMongoCollection<NotificationOutboxDocument> collection,
        NotificationOutboxDocument item, TimeSpan delay, CancellationToken ct)
    {
        var nextStatus = item.Attempts >= 3 ? "Failed" : "Pending";
        return collection.UpdateOneAsync(
            x => x.Id == item.Id && x.ProcessingStatus == "Processing" && x.WorkerId == workerId,
            Builders<NotificationOutboxDocument>.Update.Combine(
                Builders<NotificationOutboxDocument>.Update.Set(x => x.ProcessingStatus, nextStatus),
                Builders<NotificationOutboxDocument>.Update.Set(x => x.NextAttemptAt, DateTime.UtcNow + delay),
                Builders<NotificationOutboxDocument>.Update.Set<string?>(x => x.WorkerId, null),
                Builders<NotificationOutboxDocument>.Update.Set<DateTime?>(x => x.LeaseUntil, null)), cancellationToken: ct);
    }
}
