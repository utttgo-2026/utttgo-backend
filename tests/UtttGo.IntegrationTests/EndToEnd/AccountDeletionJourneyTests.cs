using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Media.Application;
using Xunit;

namespace UtttGo.IntegrationTests.EndToEnd;

[Trait("Category", "E2E")]
public sealed class AccountDeletionJourneyTests : IAsyncLifetime
{
    private const string Bucket = "utttgo-account-deletion-local";
    private IContainer localstack = null!;
    private AmazonS3Client s3 = null!;
    private EndToEndHost host = null!;

    public async Task InitializeAsync()
    {
        localstack = new ContainerBuilder("localstack/localstack:4.9")
            .WithEnvironment("SERVICES", "s3")
            .WithPortBinding(4566, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                request => request.ForPort(4566).ForPath("/_localstack/health")))
            .Build();
        await localstack.StartAsync();
        s3 = new AmazonS3Client(new Amazon.Runtime.BasicAWSCredentials("local-test", "local-test"),
            new AmazonS3Config
            {
                ServiceURL = $"http://{localstack.Hostname}:{localstack.GetMappedPublicPort(4566)}",
                ForcePathStyle = true
            });
        await s3.PutBucketAsync(new PutBucketRequest { BucketName = Bucket });
        host = await EndToEndHost.StartWithCatalogSeedAsync(services =>
        {
            services.RemoveAll<IMediaObjectStore>();
            services.AddSingleton<IMediaObjectStore>(new LocalS3Store(s3));
        });
    }

    public async Task DisposeAsync()
    {
        if (host is not null) await host.DisposeAsync();
        s3?.Dispose();
        if (localstack is not null) await localstack.DisposeAsync();
    }

    [Fact]
    public async Task ActiveOrdersReturnConflictWithoutDestructiveChanges()
    {
        var seller = await CreateSellerWithProductAsync("active-order-seller");
        var buyer = await RegisterAsync("active-order-buyer");
        using var order = await buyer.Client.PostAsJsonAsync("/api/v1/orders",
            new { productId = seller.ProductId, quantity = 1 });
        order.StatusCode.Should().Be(HttpStatusCode.Created);

        var operationId = Guid.NewGuid().ToString();
        using var deletion = await DeleteAsync(buyer.Client, operationId, Proof());

        deletion.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await buyer.Client.GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await seller.Client.GetAsync($"/api/v1/products/{seller.ProductId}")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SuccessfulDeletionCompletesAnonymizesAndPausesSellerPublications()
    {
        var seller = await CreateSellerWithProductAsync("successful-deletion");
        var operationId = Guid.NewGuid().ToString();
        var proof = Proof();
        using var deletion = await DeleteAsync(seller.Client, operationId, proof);

        deletion.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var status = await WaitForStatusAsync(operationId, proof);
        status.GetProperty("status").GetString().Should().Be(AccountDeletionOperationStates.Completed);
        status = await WaitForMediaPurgeAsync(operationId, proof);
        status.GetProperty("mediaPurgeStatus").GetProperty("status").GetString()
            .Should().Be("Completed");
        var product = await host.Database.GetCollection<MongoDB.Bson.BsonDocument>("products")
            .Find(MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id",
                MongoDB.Bson.ObjectId.Parse(seller.ProductId))).FirstAsync();
        product["status"].AsString.Should().Be("Paused");

        var user = await host.Database.GetCollection<MongoDB.Bson.BsonDocument>("users")
            .Find(MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id",
                MongoDB.Bson.ObjectId.Parse(seller.UserId))).FirstAsync();
        user["accountStatus"].AsString.Should().Be("Deleted");
        user["displayName"].AsString.Should().Be("Cuenta eliminada");
        user["email"].AsString.Should().EndWith("@invalid.utttgo.local");
    }

    [Fact]
    public async Task SuccessfulDeletionRevokesTheCurrentJwtInRedis()
    {
        var user = await RegisterAsync("revocation");
        var operationId = Guid.NewGuid().ToString();
        using var deletion = await DeleteAsync(user.Client, operationId, Proof());

        deletion.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var profile = await user.Client.GetAsync("/api/v1/users/me");
        profile.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LostDeleteResponseCanBeRecoveredByOperationStatus()
    {
        var user = await RegisterAsync("lost-response");
        var operationId = Guid.NewGuid().ToString();
        var proof = Proof();
        using (await DeleteAsync(user.Client, operationId, proof)) { }

        var status = await WaitForStatusAsync(operationId, proof);
        status.GetProperty("operationId").GetString().Should().Be(operationId);
        status.GetProperty("status").GetString().Should().Be(AccountDeletionOperationStates.Completed);
    }

    [Fact]
    public async Task RecoveryPassCompletesAStaleProcessingOperation()
    {
        var user = await RegisterAsync("stale-processing");
        using var scope = host.Services.CreateScope();
        var profileReader = scope.ServiceProvider.GetRequiredService<IAuthenticatedLocalProfileReader>();
        var profile = (await profileReader.ReadAsync(user.Token, default)).Profile!;
        var operationId = Guid.NewGuid().ToString();
        var proof = Proof();
        var store = scope.ServiceProvider.GetRequiredService<IAccountDeletionOperationStore>();
        var operation = await store.CreateOrGetAsync(operationId, AccountDeletionProof.Hash(proof),
            profile.SupabaseUserId, profile.Id, profile.SellerId, profile.Version,
            Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), default);
        operation.Should().NotBeNull();
        await store.MarkAsync(operationId, AccountDeletionOperationStates.Processing,
            AccountDeletionStages.SessionRevoked, null, default);

        await scope.ServiceProvider.GetRequiredService<IAccountDeletionRecovery>()
            .RecoverAsync(operation!, default);
        var recovered = await store.FindAsync(operationId, AccountDeletionProof.Hash(proof), default);
        recovered!.Status.Should().Be(AccountDeletionOperationStates.Completed);
    }

    [Fact]
    public async Task MediaPurgeDeletesThePhysicalS3ObjectAndReportsStatus()
    {
        var user = await RegisterAsync("media-purge");
        var assetId = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        var key = $"approved/{user.UserId}/{assetId}";
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = Bucket,
            Key = key,
            ContentBody = "local-test-image"
        });
        await host.Services.GetRequiredService<IMediaRepository>().AddAsync(
            new MediaRecord(assetId, user.UserId, key, "image/jpeg", 16, MediaStatus.Approved,
                DateTime.UtcNow), default);

        var operationId = Guid.NewGuid().ToString();
        var proof = Proof();
        using var deletion = await DeleteAsync(user.Client, operationId, proof);
        deletion.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var status = await WaitForStatusAsync(operationId, proof);
        status = await WaitForMediaPurgeAsync(operationId, proof);
        status.GetProperty("mediaPurgeStatus").GetProperty("status").GetString()
            .Should().Be("Completed");
        Func<Task> readDeletedObject = async () => await s3.GetObjectMetadataAsync(Bucket, key);
        await readDeletedObject.Should().ThrowAsync<Amazon.S3.AmazonS3Exception>();
    }

    private async Task<(HttpClient Client, string Token, string UserId)> RegisterAsync(string suffix)
    {
        var email = $"{suffix}-{Guid.NewGuid():N}@uttt.edu.mx";
        var client = host.CreateClient();
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "E2E-only-password",
            displayName = $"E2E {suffix}"
        });
        registration.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "E2E-only-password"
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = json.RootElement.GetProperty("accessToken").GetString()!;
        var userId = json.RootElement.GetProperty("user").GetProperty("id").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token, userId);
    }

    private async Task<(HttpClient Client, string Token, string UserId, string SellerId, string ProductId)>
        CreateSellerWithProductAsync(string suffix)
    {
        var seller = await RegisterAsync(suffix);
        using var activation = await seller.Client.PostAsJsonAsync("/api/v1/sellers/activation-requests",
            new { storeName = $"Store {suffix}", description = "E2E local seller", phone = "Campus" });
        activation.StatusCode.Should().Be(HttpStatusCode.Created);
        using var activationJson = JsonDocument.Parse(await activation.Content.ReadAsStringAsync());
        var sellerId = activationJson.RootElement.GetProperty("sellerId").GetString()!;
        using var categories = await seller.Client.GetAsync("/api/v1/categories");
        using var categoriesJson = JsonDocument.Parse(await categories.Content.ReadAsStringAsync());
        var categoryId = categoriesJson.RootElement.GetProperty("items")[0].GetProperty("id").GetString()!;
        using var product = await seller.Client.PostAsJsonAsync("/api/v1/products", new
        {
            name = $"Product {suffix}",
            description = "E2E local product",
            ingredients = new[] { "test" },
            categoryId,
            price = 10.00m,
            stock = 5
        });
        product.StatusCode.Should().Be(HttpStatusCode.Created);
        using var productJson = JsonDocument.Parse(await product.Content.ReadAsStringAsync());
        return (seller.Client, seller.Token, seller.UserId, sellerId,
            productJson.RootElement.GetProperty("id").GetString()!);
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string operationId, string proof)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/me");
        request.Headers.Add("X-Account-Deletion-Id", operationId);
        request.Headers.Add("X-Account-Deletion-Proof", proof);
        return await client.SendAsync(request);
    }

    private async Task<JsonElement> WaitForStatusAsync(string operationId, string proof)
    {
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Account-Deletion-Proof", proof);
        for (var attempt = 0; attempt < 30; attempt++)
        {
            using var response = await client.GetAsync($"/api/v1/users/me/deletion/{operationId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var status = document.RootElement.Clone();
                if (status.GetProperty("status").GetString() == AccountDeletionOperationStates.Completed)
                    return status;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"Deletion operation {operationId} did not complete locally.");
    }

    private async Task<JsonElement> WaitForMediaPurgeAsync(string operationId, string proof)
    {
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Account-Deletion-Proof", proof);
        for (var attempt = 0; attempt < 40; attempt++)
        {
            using var response = await client.GetAsync($"/api/v1/users/me/deletion/{operationId}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var status = document.RootElement.Clone();
                if (status.GetProperty("mediaPurgeStatus").GetProperty("status").GetString() == "Completed")
                    return status;
            }
            await Task.Delay(250);
        }
        throw new TimeoutException($"Media purge for operation {operationId} did not complete locally.");
    }

    private static string Proof() => Convert.ToHexString(Guid.NewGuid().ToByteArray()) +
        Convert.ToHexString(Guid.NewGuid().ToByteArray());

    private sealed class LocalS3Store(AmazonS3Client client) : IMediaObjectStore
    {
        public Task<(string Url, DateTime ExpiresAt, IReadOnlyDictionary<string, string> Headers)> SignPutAsync(
            string key, string mime, CancellationToken cancellationToken) =>
            Task.FromResult<(string, DateTime, IReadOnlyDictionary<string, string>)>(
                ("http://local.test/put", DateTime.UtcNow.AddMinutes(4),
                    new Dictionary<string, string> { ["Content-Type"] = mime }));

        public async Task<(long Length, string Mime)?> HeadAsync(string key, CancellationToken cancellationToken)
        {
            try
            {
                var metadata = await client.GetObjectMetadataAsync(Bucket, key, cancellationToken);
                return (metadata.ContentLength, metadata.Headers.ContentType);
            }
            catch (Amazon.S3.AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            { return null; }
        }

        public Task<MediaReadUrl> SignGetAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(new MediaReadUrl("http://local.test/get", DateTime.UtcNow.AddMinutes(4)));

        public Task CopyAsync(string sourceKey, string targetKey, string mime, CancellationToken cancellationToken) =>
            client.CopyObjectAsync(new CopyObjectRequest
            {
                SourceBucket = Bucket,
                SourceKey = sourceKey,
                DestinationBucket = Bucket,
                DestinationKey = targetKey,
                ContentType = mime,
                MetadataDirective = S3MetadataDirective.REPLACE
            }, cancellationToken);

        public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
            client.DeleteObjectAsync(Bucket, key, cancellationToken);
    }
}
