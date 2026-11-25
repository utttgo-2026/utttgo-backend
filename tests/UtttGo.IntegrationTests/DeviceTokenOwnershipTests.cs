using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Notifications.Infrastructure;
using UTTTGO.Api.Shared.Infrastructure;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class DeviceTokenOwnershipTests
{
    [Fact]
    public async Task RegisteringAnExistingTokenDeactivatesItsPreviousOwner()
    {
        var uri = Environment.GetEnvironmentVariable("UTTTGO_TEST_MONGODB_URI")
            ?? "mongodb://127.0.0.1:27017/?directConnection=true";
        var services = new ServiceCollection()
            .AddSingleton<IMongoClient>(_ => new MongoClient(uri))
            .BuildServiceProvider();
        var options = Options.Create(new MongoOptions { Uri = uri, Database = "utttgo-token-test" });
        var store = new MongoDeviceTokenStore(new MongoStore(services, options),
            DataProtectionProvider.Create("utttgo-token-test"));

        await store.UpsertAsync("owner-a", "same-fcm-token", "install-a", "android", "1.0", CancellationToken.None);
        await store.UpsertAsync("owner-b", "same-fcm-token", "install-b", "android", "1.0", CancellationToken.None);

        var documents = await services.GetRequiredService<IMongoClient>()
            .GetDatabase("utttgo-token-test")
            .GetCollection<DeviceTokenDocument>("deviceTokens")
            .Find(FilterDefinition<DeviceTokenDocument>.Empty)
            .ToListAsync();

        documents.Should().ContainSingle(item => item.UserId == "owner-a" && !item.Active);
        documents.Should().ContainSingle(item => item.UserId == "owner-b" && item.Active);
    }
}
