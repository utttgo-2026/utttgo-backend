using System.Text.Json;
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
            if (string.IsNullOrWhiteSpace(json) && string.IsNullOrWhiteSpace(path)) { logger.LogWarning("Firebase credentials are not configured; push is disabled."); return null; }
            var credential = string.IsNullOrWhiteSpace(json) ? GoogleCredential.FromFile(path!) : GoogleCredential.FromJson(json);
            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions { Credential = credential });
            return FirebaseMessaging.GetMessaging(app);
        }
        catch { logger.LogWarning("Firebase credentials could not be loaded; push is disabled."); return null; }
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
                .SetOnInsert(x => x.CorrelationId, correlationId).SetOnInsert(x => x.IdempotencyKey, idempotencyKey).SetOnInsert(x => x.Attempts, 0)
                .SetOnInsert(x => x.CreatedAt, DateTime.UtcNow), new UpdateOptions { IsUpsert = true }, ct);
}
public sealed class NotificationOutboxDocument
{
    [MongoDB.Bson.Serialization.Attributes.BsonId] public ObjectId Id { get; set; }
    public string EventType { get; set; } = ""; public string RecipientUserId { get; set; } = ""; public string OrderId { get; set; } = "";
    public string Status { get; set; } = ""; public string CorrelationId { get; set; } = ""; public string IdempotencyKey { get; set; } = "";
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
public sealed class NotificationMongoIndexes(MongoStore store, ILogger<NotificationMongoIndexes> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct) { try { var d = store.Database.GetCollection<DeviceTokenDocument>("deviceTokens"); var k = Builders<DeviceTokenDocument>.IndexKeys; await d.Indexes.CreateManyAsync([new CreateIndexModel<DeviceTokenDocument>(k.Ascending(x => x.UserId).Ascending(x => x.TokenHash), new CreateIndexOptions { Unique = true, Name = "user_tokenHash_unique" }), new CreateIndexModel<DeviceTokenDocument>(k.Ascending(x => x.UserId).Ascending(x => x.InstallationId), new CreateIndexOptions { Unique = true, Name = "user_installation_unique" }), new CreateIndexModel<DeviceTokenDocument>(k.Ascending(x => x.UserId).Ascending(x => x.Active), new CreateIndexOptions { Name = "user_active" })], ct); } catch { logger.LogWarning("Notification indexes could not be created; API startup will continue."); } }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

public sealed class NotificationOutboxDispatcher(MongoStore store, IDeviceTokenStore tokens,
    INotificationSender sender, ILogger<NotificationOutboxDispatcher> logger) : BackgroundService
{
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
        var now = DateTime.UtcNow;
        var events = await collection.Find(x => x.Attempts < 3 &&
            (x.NextAttemptAt == null || x.NextAttemptAt <= now)).Limit(50).ToListAsync(ct);
        foreach (var item in events)
        {
            var devices = await tokens.FindActiveAsync(item.RecipientUserId, ct);
            if (devices.Count == 0)
            {
                await collection.DeleteOneAsync(x => x.Id == item.Id, ct);
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

            if (!transient || disabled || item.Attempts >= 2)
            {
                await collection.DeleteOneAsync(x => x.Id == item.Id, ct);
                continue;
            }

            var delay = item.Attempts switch { 0 => TimeSpan.FromSeconds(1), 1 => TimeSpan.FromSeconds(5), _ => TimeSpan.FromSeconds(30) };
            await collection.UpdateOneAsync(x => x.Id == item.Id,
                Builders<NotificationOutboxDocument>.Update.Inc(x => x.Attempts, 1)
                    .Set(x => x.NextAttemptAt, DateTime.UtcNow.Add(delay)), cancellationToken: ct);
        }
    }
}
