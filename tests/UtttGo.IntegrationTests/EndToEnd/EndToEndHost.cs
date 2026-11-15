using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Shared.Infrastructure;

namespace UtttGo.IntegrationTests.EndToEnd;

internal sealed class RemoteIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            await nextMiddleware();
        });
        next(app);
    };
}

internal sealed class EndToEndHost : IAsyncDisposable
{
    public const string DatabaseName = "utttgo_e2e";

    private readonly IContainer mongo;
    private readonly IContainer redis;
    private readonly WebApplicationFactory<Program> factory;

    private EndToEndHost(IContainer mongo, IContainer redis, WebApplicationFactory<Program> factory)
    {
        this.mongo = mongo;
        this.redis = redis;
        this.factory = factory;
        Client = factory.CreateClient();
        Services = factory.Services;
        Database = Services.GetRequiredService<IMongoClient>().GetDatabase(DatabaseName);
    }

    public HttpClient Client { get; }

    public IServiceProvider Services { get; }

    public IMongoDatabase Database { get; }

    public static async Task<EndToEndHost> StartAsync()
    {
        var mongo = new ContainerBuilder("mongo:7")
            .WithPortBinding(27017, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(27017))
            .Build();
        var redis = new ContainerBuilder("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(6379))
            .Build();

        await mongo.StartAsync();
        await redis.StartAsync();

        var mongoUri = $"mongodb://{mongo.Hostname}:{mongo.GetMappedPublicPort(27017)}";
        var redisUri = $"redis://{redis.Hostname}:{redis.GetMappedPublicPort(6379)}";
        await WaitForMongoAsync(mongoUri);
        await WaitForRedisAsync(redisUri);

        var auth = new EndToEndAuthDoubles();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOptions<MongoOptions>>();
                services.AddSingleton<IOptions<MongoOptions>>(Options.Create(new MongoOptions
                {
                    Uri = mongoUri,
                    Database = DatabaseName
                }));
                services.RemoveAll<IConnectionMultiplexer>();
                services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisUri));
                services.AddSingleton<IStartupFilter, RemoteIpStartupFilter>();
                services.RemoveAll<ISupabaseRegistration>();
                services.AddSingleton<ISupabaseRegistration>(auth);
                services.RemoveAll<ISupabaseLogin>();
                services.AddSingleton<ISupabaseLogin>(auth);
                services.RemoveAll<IValidatedJwtSubjectReader>();
                services.AddSingleton<IValidatedJwtSubjectReader>(auth);
                services.RemoveAll<ILoginJwtGate>();
                services.AddSingleton<ILoginJwtGate>(auth);
                services.RemoveAll<ISupabaseSessionIdentity>();
                services.AddSingleton<ISupabaseSessionIdentity>(auth);
            }));

        return new EndToEndHost(mongo, redis, factory);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await factory.DisposeAsync();
        await redis.DisposeAsync();
        await mongo.DisposeAsync();
    }

    private static async Task WaitForMongoAsync(string uri)
    {
        var client = new MongoClient(uri);
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
                return;
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(500);
            }
        }

        throw new InvalidOperationException("MongoDB container did not answer the ping command.");
    }

    private static async Task WaitForRedisAsync(string uri)
    {
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var multiplexer = await ConnectionMultiplexer.ConnectAsync(uri);
                await multiplexer.GetDatabase().PingAsync();
                await multiplexer.DisposeAsync();
                return;
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(500);
            }
        }

        throw new InvalidOperationException("Redis container did not answer the ping command.");
    }
}
