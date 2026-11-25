using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Notifications.Application;
using UTTTGO.Api.Modules.Notifications.Infrastructure;
using UtttGo.IntegrationTests.EndToEnd;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class NotificationOutboxConcurrencyTests
{
    [Fact]
    public async Task TwoDispatchersClaimAnEventOnlyOnce()
    {
        await using var host = await EndToEndHost.StartAsync();
        var mongo = host.Services.GetRequiredService<UTTTGO.Api.Shared.Infrastructure.MongoStore>();
        var collection = host.Database.GetCollection<NotificationOutboxDocument>("notificationOutbox");
        var eventId = ObjectId.GenerateNewId();
        await collection.InsertOneAsync(new NotificationOutboxDocument
        {
            Id = eventId,
            EventType = "order.status.changed",
            RecipientUserId = "owner-user",
            OrderId = "order-1",
            Status = "Ready",
            CorrelationId = "correlation",
            IdempotencyKey = "order.status.changed:order-1:Ready",
            ProcessingStatus = "Pending",
            CreatedAt = DateTime.UtcNow
        });

        var sender = new RecordingSender();
        var tokens = new RecordingTokens();
        var first = new NotificationOutboxDispatcher(mongo, tokens, sender,
            NullLogger<NotificationOutboxDispatcher>.Instance);
        var second = new NotificationOutboxDispatcher(mongo, tokens, sender,
            NullLogger<NotificationOutboxDispatcher>.Instance);
        await first.StartAsync(CancellationToken.None);
        await second.StartAsync(CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(3));
        await first.StopAsync(CancellationToken.None);
        await second.StopAsync(CancellationToken.None);

        sender.Messages.Should().Be(1);
        (await collection.Find(x => x.Id == eventId).SingleAsync()).ProcessingStatus.Should().Be("Sent");
    }

    private sealed class RecordingSender : INotificationSender
    {
        public int Messages;

        public Task<NotificationSendResult> SendAsync(NotificationMessage message, string token,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Messages);
            return Task.FromResult(new NotificationSendResult(NotificationSendStatus.Sent));
        }
    }

    private sealed class RecordingTokens : IDeviceTokenStore
    {
        private static readonly IReadOnlyList<DeviceTokenRecord> Records =
            [new("device-1", "owner-user", "protected-token", "hash")];

        public Task<DeviceTokenRegistration> UpsertAsync(string userId, string token, string installationId,
            string platform, string? appVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RemoveAsync(string userId, string deviceTokenId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RemoveForUserAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DeviceTokenRecord>> FindActiveAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Records);
        public Task DeactivateAsync(string deviceTokenId, CancellationToken cancellationToken) => Task.CompletedTask;
        public string Unprotect(string value) => "fcm-token";
    }
}
