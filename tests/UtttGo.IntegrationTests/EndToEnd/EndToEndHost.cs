using System.Net;
using DotNet.Testcontainers.Builders;
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
using Testcontainers.MongoDb;
using Testcontainers.Redis;
using UTTTGO.Api.Modules.Catalog.Infrastructure;
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

    private const int StartAttempts = 3;
    private const int SeedAttempts = 3;
    private const ushort MongoDbPort = 27017;
    private const ushort RedisPort = 6379;

    private readonly MongoDbContainer mongo;
    private readonly RedisContainer redis;
    private readonly IConnectionMultiplexer redisConnection;
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory;

    private EndToEndHost(MongoDbContainer mongo, RedisContainer redis, IConnectionMultiplexer redisConnection,
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        this.mongo = mongo;
        this.redis = redis;
        this.redisConnection = redisConnection;
        this.factory = factory;
        Client = factory.CreateClient();
        Services = factory.Services;
        Database = Services.GetRequiredService<IMongoClient>().GetDatabase(DatabaseName);
    }

    public HttpClient Client { get; }

    // A fresh client per identity: the Authorization header is never shared.
    public HttpClient CreateClient() => factory.CreateClient();

    public IServiceProvider Services { get; }

    public IMongoDatabase Database { get; }

    public static async Task<EndToEndHost> StartAsync(Action<IServiceCollection>? configureServices = null)
    {
        for (var attempt = 1; attempt <= StartAttempts; attempt++)
        {
            try
            {
                return await StartOnceAsync(configureServices);
            }
            catch (Exception) when (attempt < StartAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
            }
        }

        throw new InvalidOperationException("The end-to-end host could not be started.");
    }

    public static async Task<EndToEndHost> StartWithCatalogSeedAsync(
        Action<IServiceCollection>? configureServices = null)
    {
        var host = await StartAsync(configureServices);
        try
        {
            await SeedCatalogAsync(host);
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await factory.DisposeAsync();
        await redisConnection.DisposeAsync();
        await redis.DisposeAsync();
        await mongo.DisposeAsync();
    }

    private static async Task<EndToEndHost> StartOnceAsync(Action<IServiceCollection>? configureServices)
    {
        var mongo = BuildMongoContainer();
        var redis = BuildRedisContainer();
        IConnectionMultiplexer? redisConnection = null;
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>? factory = null;

        try
        {
            await mongo.StartAsync();
            await redis.StartAsync();

            var mongoUri = BuildMongoUri(mongo);
            var redisHost = redis.Hostname;
            var redisPort = redis.GetMappedPublicPort(RedisPort);
            await WaitForMongoAsync(mongoUri);
            await WaitForRedisAsync(redisHost, redisPort);

            redisConnection = CreateMultiplexer(redisHost, redisPort);
            var auth = new EndToEndAuthDoubles(redisConnection);
            factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOptions<MongoOptions>>();
                    services.AddSingleton<IOptions<MongoOptions>>(Options.Create(new MongoOptions
                    {
                        Uri = mongoUri,
                        Database = DatabaseName
                    }));
                    services.RemoveAll<IConnectionMultiplexer>();
                    services.AddSingleton(redisConnection);
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
                    configureServices?.Invoke(services);
                }));

            return new EndToEndHost(mongo, redis, redisConnection, factory);
        }
        catch
        {
            if (factory is not null) await factory.DisposeAsync();
            if (redisConnection is not null) await redisConnection.DisposeAsync();
            await redis.DisposeAsync();
            await mongo.DisposeAsync();
            throw;
        }
    }

    private static MongoDbContainer BuildMongoContainer() =>
        new MongoDbBuilder("mongo:7")
            .WithUsername(string.Empty)
            .WithPassword(string.Empty)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilExternalTcpPortIsAvailable(MongoDbPort)
                .UntilCommandIsCompleted("mongosh", "--quiet", "--eval",
                    "quit(db.adminCommand({ ping: 1 }).ok ? 0 : 1)"))
            .Build();

    private static RedisContainer BuildRedisContainer() =>
        new RedisBuilder("redis:7-alpine")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(RedisPort))
            .Build();

    private static string BuildMongoUri(MongoDbContainer mongo) =>
        $"mongodb://{mongo.Hostname}:{mongo.GetMappedPublicPort(MongoDbPort)}/?directConnection=true";

    private static async Task SeedCatalogAsync(EndToEndHost host)
    {
        var seeder = host.Services.GetRequiredService<ICatalogSeeder>();
        for (var attempt = 1; attempt <= SeedAttempts; attempt++)
        {
            try
            {
                await seeder.SeedAsync(CancellationToken.None);
                return;
            }
            catch (Exception) when (attempt < SeedAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
            }
        }
    }

    private static IConnectionMultiplexer CreateMultiplexer(string host, int port)
    {
        var configuration = CreateRedisConfiguration(host, port);
        return ConnectionMultiplexer.Connect(configuration);
    }

    // abortConnect=false: the multiplexer must not throw while the container is still starting.
    private static ConfigurationOptions CreateRedisConfiguration(string host, int port)
    {
        var configuration = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = 2000,
            SyncTimeout = 5000
        };
        configuration.EndPoints.Add(host, port);
        return configuration;
    }

    private static async Task WaitForMongoAsync(string uri)
    {
        var settings = MongoClientSettings.FromConnectionString(uri);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        var deadline = DateTime.UtcNow.AddSeconds(90);
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

    private static async Task WaitForRedisAsync(string host, int port)
    {
        var configuration = CreateRedisConfiguration(host, port);
        await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(configuration);
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await multiplexer.GetDatabase().PingAsync();
                return;
            }
            catch (Exception) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(500);
            }
        }

        throw new InvalidOperationException(
            $"Redis container did not answer the ping command on {host}:{port}.");
    }
}
