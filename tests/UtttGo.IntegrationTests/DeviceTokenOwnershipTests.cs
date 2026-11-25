using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Notifications.Infrastructure;
using UTTTGO.Api.Shared.Infrastructure;
using UtttGo.IntegrationTests.EndToEnd;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class DeviceTokenOwnershipTests
{
    [Fact]
    public async Task RegisteringAnExistingTokenDeactivatesItsPreviousOwner()
    {
        await using var host = await EndToEndHost.StartAsync();
        var store = new MongoDeviceTokenStore(host.Services.GetRequiredService<MongoStore>(),
            DataProtectionProvider.Create("utttgo-token-test"));

        await store.UpsertAsync("owner-a", "same-fcm-token", "install-a", "android", "1.0", CancellationToken.None);
        await store.UpsertAsync("owner-b", "same-fcm-token", "install-b", "android", "1.0", CancellationToken.None);

        var documents = await host.Database
            .GetCollection<DeviceTokenDocument>("deviceTokens")
            .Find(FilterDefinition<DeviceTokenDocument>.Empty)
            .ToListAsync();

        documents.Should().ContainSingle(item => item.UserId == "owner-a" && !item.Active);
        documents.Should().ContainSingle(item => item.UserId == "owner-b" && item.Active);
    }
}
