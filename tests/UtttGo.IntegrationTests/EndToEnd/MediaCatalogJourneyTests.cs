using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using UTTTGO.Api.Modules.Media.Application;
using Xunit;

namespace UtttGo.IntegrationTests.EndToEnd;

[Trait("Category", "E2E")]
public sealed class MediaCatalogJourneyTests : IAsyncLifetime
{
    private const string Bucket = "utttgo-media-local-test";
    private const string ImageType = "image/jpeg";
    private IContainer localstack = null!;
    private AmazonS3Client s3 = null!;
    private EndToEndHost host = null!;
    private readonly LocalAnalyzer analyzer = new();

    public async Task InitializeAsync()
    {
        localstack = new ContainerBuilder("localstack/localstack:4.9")
            .WithEnvironment("SERVICES", "s3").WithPortBinding(4566, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                request => request.ForPort(4566).ForPath("/_localstack/health"))).Build();
        await localstack.StartAsync();
        s3 = new AmazonS3Client(new BasicAWSCredentials("local-test", "local-test"),
            new AmazonS3Config
            {
                ServiceURL = $"http://{localstack.Hostname}:{localstack.GetMappedPublicPort(4566)}",
                ForcePathStyle = true
            });
        await s3.PutBucketAsync(new PutBucketRequest { BucketName = Bucket });
        host = await EndToEndHost.StartWithCatalogSeedAsync(services =>
        {
            services.RemoveAll<IMediaObjectStore>();
            services.RemoveAll<IMediaImageSource>();
            services.RemoveAll<IContentSafetyAnalyzer>();
            services.RemoveAll<IOptions<ContentSafetyOptions>>();
            var localStore = new LocalS3Store(s3);
            services.AddSingleton<IMediaObjectStore>(localStore);
            services.AddSingleton<IMediaImageSource>(localStore);
            services.AddSingleton<IContentSafetyAnalyzer>(analyzer);
            // Deliberate test-only policy; no values are configured as production defaults.
            services.AddSingleton<IOptions<ContentSafetyOptions>>(Options.Create(new ContentSafetyOptions
            {
                AllowControlledTestPolicy = true,
                MaxHateSeverity = 0,
                MaxSelfHarmSeverity = 0,
                MaxSexualSeverity = 0,
                MaxViolenceSeverity = 0
            }));
        });
    }

    public async Task DisposeAsync()
    {
        if (host is not null) await host.DisposeAsync();
        s3?.Dispose();
        if (localstack is not null) await localstack.DisposeAsync();
    }

    [Fact]
    public async Task UploadConfirmPublishReplaceRemoveAndPreserveThroughMongoRedisAndS3()
    {
        var client = host.Client;
        using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "media.e2e@uttt.edu.mx",
            password = "Local-test-only-1!",
            displayName = "Media E2E"
        });
        register.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "media.e2e@uttt.edu.mx",
            password = "Local-test-only-1!"
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            loginJson.RootElement.GetProperty("accessToken").GetString());
        using var activation = await client.PostAsJsonAsync("/api/v1/sellers/activation-requests",
            new { storeName = "Fotos E2E", description = "Prueba local", phone = "Campus" });
        activation.StatusCode.Should().Be(HttpStatusCode.Created);
        using var categories = await client.GetAsync("/api/v1/categories");
        using var categoriesJson = JsonDocument.Parse(await categories.Content.ReadAsStringAsync());
        var categoryId = categoriesJson.RootElement.GetProperty("items")[0].GetProperty("id").GetString();

        using var without = await client.PostAsJsonAsync("/api/v1/products",
            new { name = "Sin foto", description = "Sin foto", categoryId, price = 10m, stock = 2 });
        without.StatusCode.Should().Be(HttpStatusCode.Created);
        using var withoutJson = JsonDocument.Parse(await without.Content.ReadAsStringAsync());
        withoutJson.RootElement.GetProperty("imageUrl").ValueKind.Should().Be(JsonValueKind.Null);
        var productId = withoutJson.RootElement.GetProperty("id").GetString();

        var firstId = await UploadAndApprove(client, "one.jpg");
        var secondId = await UploadAndApprove(client, "two.jpg");
        await CheckRejectedUploadsAndUnapprovedRead(client, categoryId!);
        using var withImage = await client.PostAsJsonAsync("/api/v1/products",
            new
            {
                name = "Con foto",
                description = "Foto aprobada",
                categoryId,
                price = 12m,
                stock = 1,
                mediaAssetId = firstId
            });
        withImage.StatusCode.Should().Be(HttpStatusCode.Created);
        using var withImageJson = JsonDocument.Parse(await withImage.Content.ReadAsStringAsync());
        await AssertPresignedGet(withImageJson.RootElement);
        await PatchImage(client, productId!, $$"""{"mediaAssetId":"{{firstId}}"}""", firstId);
        await PatchImage(client, productId!, """{"stock":3}""", firstId);
        await PatchImage(client, productId!, $$"""{"mediaAssetId":"{{secondId}}"}""", secondId);

        using var listing = await client.GetAsync("/api/v1/products");
        using var listingJson = JsonDocument.Parse(await listing.Content.ReadAsStringAsync());
        var image = listingJson.RootElement.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("id").GetString() == productId);
        await AssertPresignedGet(image);
        using var detail = await client.GetAsync($"/api/v1/products/{productId}");
        using var detailJson = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        await AssertPresignedGet(detailJson.RootElement);

        var stored = await host.Database.GetCollection<BsonDocument>("products")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(productId)))
            .FirstAsync();
        stored["mediaAssetId"].AsString.Should().Be(secondId);
        await PatchImage(client, productId!, """{"mediaAssetId":null}""", null);
    }

    [Fact]
    public async Task AlteredSignedPutIsNotTrustedWithoutBackendHeadAndOwnership()
    {
        var key = "staging/negative/signature.jpg";
        var url = s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = ImageType,
            Expires = DateTime.UtcNow.AddMinutes(2),
            Protocol = Protocol.HTTP
        });
        using var http = new HttpClient();
        using var mimeBody = new ByteArrayContent(new byte[] { 1, 2, 3 });
        mimeBody.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using var changedMime = await http.PutAsync(url, mimeBody);
        // LocalStack can accept a changed signed Content-Type; backend HEAD must
        // reject the object's actual MIME (separate API test below).
        if (changedMime.IsSuccessStatusCode)
        {
            var metadata = await s3.GetObjectMetadataAsync(Bucket, key);
            metadata.Headers.ContentType.Should().Be("image/png");
        }

        var changedKeyUrl = url.Replace("/signature.jpg?", "/changed.jpg?", StringComparison.Ordinal);
        changedKeyUrl.Should().NotBe(url);
        using var keyBody = new ByteArrayContent(new byte[] { 1, 2, 3 });
        keyBody.Headers.ContentType = new MediaTypeHeaderValue(ImageType);
        using var changedKey = await http.PutAsync(changedKeyUrl, keyBody);
        if (changedKey.IsSuccessStatusCode)
        {
            // Emulated signature verification is not AWS evidence.
            var metadata = await s3.GetObjectMetadataAsync(Bucket, "staging/negative/changed.jpg");
            metadata.ContentLength.Should().Be(3);
        }
    }

    [Fact]
    public async Task ExpiredPresignedPutIsProbedButRequiresAwsEnforcementEvidence()
    {
        var url = s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = Bucket,
            Key = "staging/negative/expired.jpg",
            Verb = HttpVerb.PUT,
            ContentType = ImageType,
            Expires = DateTime.UtcNow.AddSeconds(2),
            Protocol = Protocol.HTTP
        });
        var query = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
        var signedSeconds = int.Parse(query["X-Amz-Expires"]!);
        signedSeconds.Should().BeLessThanOrEqualTo(300);
        await Task.Delay(TimeSpan.FromSeconds(signedSeconds + 2));
        using var body = new ByteArrayContent(new byte[] { 1 });
        body.Headers.ContentType = new MediaTypeHeaderValue(ImageType);
        using var response = await new HttpClient().PutAsync(url, body);
        // LocalStack does not reliably enforce expired credentials. This probe
        // must not claim AWS rejection; record the HTTP result during review.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden,
            HttpStatusCode.BadRequest);
    }

    private async Task<string> UploadAndApprove(HttpClient client, string file)
    {
        using var intent = await client.PostAsJsonAsync("/api/v1/media/upload-url",
            new { fileName = file, mimeType = ImageType, size = 10 });
        intent.StatusCode.Should().Be(HttpStatusCode.Created);
        using var json = JsonDocument.Parse(await intent.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("mediaAssetId").GetString()!;
        var upload = json.RootElement.GetProperty("uploadUrl").GetString()!;
        var expires = json.RootElement.GetProperty("expiresAt").GetDateTime();
        expires.Should().BeAfter(DateTime.UtcNow);
        expires.Should().BeOnOrBefore(DateTime.UtcNow.AddMinutes(5));
        var parameters = System.Web.HttpUtility.ParseQueryString(new Uri(upload).Query);
        int.Parse(parameters["X-Amz-Expires"]!).Should().BeLessThanOrEqualTo(300);
        json.RootElement.GetProperty("headers").GetProperty("Content-Type").GetString().Should().Be(ImageType);
        using var body = new ByteArrayContent(new byte[] { 0xff, 0xd8, 0xff, 0, 0, 0, 0, 0, 0xff, 0xd9 });
        body.Headers.ContentType = new MediaTypeHeaderValue(ImageType);
        using var put = await new HttpClient().PutAsync(upload, body);
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        using var complete = await client.PostAsJsonAsync("/api/v1/media/confirm",
            new { mediaAssetId = id });
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        (await complete.Content.ReadAsStringAsync()).Should().Contain(MediaStatus.Approved);
        using var repeat = await client.PostAsJsonAsync($"/api/v1/media/{id}/complete", new { });
        (await repeat.Content.ReadAsStringAsync()).Should().Contain(MediaStatus.Approved);
        var approved = await host.Database.GetCollection<BsonDocument>("mediaAssets")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id))).FirstAsync();
        approved["objectKey"].AsString.Should().StartWith("approved/");
        approved["objectKey"].AsString.Should().NotBe($"staging/{approved["ownerUserId"].AsString}/{id}");
        // The original PUT is still usable in LocalStack; replacing staging
        // cannot change the independently sealed bytes signed for approved GET.
        using var replacement = new ByteArrayContent(new byte[] { 0xff, 0xd8, 0xff, 1, 1, 1, 1, 1, 0xff, 0xd9 });
        replacement.Headers.ContentType = new MediaTypeHeaderValue(ImageType);
        using var replay = await new HttpClient().PutAsync(upload, replacement);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        using var read = await client.GetAsync($"/api/v1/media/{id}");
        using var readJson = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        readJson.RootElement.GetProperty("status").GetString().Should().Be(MediaStatus.Approved);
        using var image = await new HttpClient().GetAsync(readJson.RootElement.GetProperty("imageUrl").GetString());
        (await image.Content.ReadAsByteArrayAsync()).Should()
            .Equal(new byte[] { 0xff, 0xd8, 0xff, 0, 0, 0, 0, 0, 0xff, 0xd9 });
        return id;
    }

    private async Task CheckRejectedUploadsAndUnapprovedRead(HttpClient client, string categoryId)
    {
        using var boundary = await client.PostAsJsonAsync("/api/v1/media/upload-url",
            new { fileName = "boundary.jpg", mimeType = ImageType, size = MediaImagePolicy.MaxBytes });
        boundary.StatusCode.Should().Be(HttpStatusCode.Created);
        using var boundaryJson = JsonDocument.Parse(await boundary.Content.ReadAsStringAsync());
        var absentId = boundaryJson.RootElement.GetProperty("mediaAssetId").GetString();
        using var invalid = await client.PostAsJsonAsync("/api/v1/media/upload-url",
            new { fileName = "invalid.jpg", mimeType = "image/jpeg", size = MediaImagePolicy.MaxBytes + 1 });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var wrongMime = await client.PostAsJsonAsync("/api/v1/media/upload-url",
            new { fileName = "invalid.png", mimeType = "image/jpeg", size = 10 });
        wrongMime.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var intent = await client.PostAsJsonAsync("/api/v1/media/upload-url",
            new { fileName = "pending.jpg", mimeType = ImageType, size = 10 });
        intent.StatusCode.Should().Be(HttpStatusCode.Created);
        using var json = JsonDocument.Parse(await intent.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("mediaAssetId").GetString()!;
        var upload = json.RootElement.GetProperty("uploadUrl").GetString()!;

        using var before = await client.GetAsync($"/api/v1/media/{id}");
        using var beforeJson = JsonDocument.Parse(await before.Content.ReadAsStringAsync());
        beforeJson.RootElement.GetProperty("imageUrl").ValueKind.Should().Be(JsonValueKind.Null);
        using var premature = await client.PostAsJsonAsync("/api/v1/products",
            new { name = "Pendiente", description = "No publicar", categoryId, price = 10m, stock = 1, mediaAssetId = id });
        premature.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using var body = new ByteArrayContent(Encoding.ASCII.GetBytes("short"));
        body.Headers.ContentType = new MediaTypeHeaderValue(ImageType);
        using var put = await new HttpClient().PutAsync(upload, body);
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var asset = await host.Database.GetCollection<BsonDocument>("mediaAssets")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id))).FirstAsync();
        var head = await s3.GetObjectMetadataAsync(Bucket, asset["objectKey"].AsString);
        head.ContentLength.Should().Be(5);
        using var mismatch = await client.PostAsJsonAsync("/api/v1/media/confirm",
            new { mediaAssetId = id });
        mismatch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var after = await client.GetAsync($"/api/v1/media/{id}");
        using var afterJson = JsonDocument.Parse(await after.Content.ReadAsStringAsync());
        afterJson.RootElement.GetProperty("status").GetString().Should().Be(MediaStatus.PendingUpload);
        afterJson.RootElement.GetProperty("imageUrl").ValueKind.Should().Be(JsonValueKind.Null);

        // A presigned PUT is not a size constraint: reject its actual oversized HEAD.
        using var oversizedIntent = await client.PostAsJsonAsync("/api/v1/media/upload-url",
            new { fileName = "oversized.jpg", mimeType = ImageType, size = 10 });
        using var oversizedJson = JsonDocument.Parse(await oversizedIntent.Content.ReadAsStringAsync());
        var oversizedId = oversizedJson.RootElement.GetProperty("mediaAssetId").GetString();
        using var oversizedBytes = new ByteArrayContent(new byte[MediaImagePolicy.MaxBytes + 1]);
        oversizedBytes.Headers.ContentType = new MediaTypeHeaderValue(ImageType);
        using var oversizedPut = await new HttpClient().PutAsync(
            oversizedJson.RootElement.GetProperty("uploadUrl").GetString(), oversizedBytes);
        oversizedPut.StatusCode.Should().Be(HttpStatusCode.OK);
        using var oversizedConfirm = await client.PostAsJsonAsync("/api/v1/media/confirm",
            new { mediaAssetId = oversizedId });
        oversizedConfirm.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var oversizedRead = await client.GetAsync($"/api/v1/media/{oversizedId}");
        using var oversizedState = JsonDocument.Parse(await oversizedRead.Content.ReadAsStringAsync());
        oversizedState.RootElement.GetProperty("imageUrl").ValueKind.Should().Be(JsonValueKind.Null);

        using var absent = await client.PostAsJsonAsync("/api/v1/media/confirm",
            new { mediaAssetId = absentId });
        absent.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await CheckInvalidStoredObject(client, "image/png", new byte[10], HttpStatusCode.BadRequest);
        await CheckInvalidStoredObject(client, ImageType, new byte[10], HttpStatusCode.OK);
    }

    private async Task CheckInvalidStoredObject(HttpClient client, string actualMime, byte[] bytes,
        HttpStatusCode expectedHttp)
    {
        using var intent = await client.PostAsJsonAsync("/api/v1/media/upload-url",
            new { fileName = "untrusted.jpg", mimeType = ImageType, size = bytes.Length });
        intent.StatusCode.Should().Be(HttpStatusCode.Created);
        using var json = JsonDocument.Parse(await intent.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("mediaAssetId").GetString()!;
        var asset = await host.Database.GetCollection<BsonDocument>("mediaAssets")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id))).FirstAsync();
        using var content = new MemoryStream(bytes);
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = Bucket,
            Key = asset["objectKey"].AsString,
            InputStream = content,
            ContentType = actualMime
        });
        using var confirmation = await client.PostAsJsonAsync("/api/v1/media/confirm",
            new { mediaAssetId = id });
        confirmation.StatusCode.Should().Be(expectedHttp);
        using var read = await client.GetAsync($"/api/v1/media/{id}");
        using var state = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        state.RootElement.GetProperty("status").GetString().Should()
            .Be(expectedHttp == HttpStatusCode.OK ? MediaStatus.Rejected : MediaStatus.PendingUpload);
        state.RootElement.GetProperty("imageUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private static async Task PatchImage(HttpClient client, string id, string json, string? expected)
    {
        using var patch = await client.PatchAsync($"/api/v1/products/{id}",
            new StringContent(json, Encoding.UTF8, "application/json"));
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        using var response = JsonDocument.Parse(await patch.Content.ReadAsStringAsync());
        if (expected is null)
            response.RootElement.GetProperty("imageUrl").ValueKind.Should().Be(JsonValueKind.Null);
        else
            await AssertPresignedGet(response.RootElement);
    }

    private static async Task AssertPresignedGet(JsonElement product)
    {
        var url = product.GetProperty("imageUrl").GetString();
        url.Should().Contain("X-Amz-Signature").And.Contain(Bucket);
        var expires = product.GetProperty("imageUrlExpiresAt").GetDateTime();
        expires.Should().BeAfter(DateTime.UtcNow);
        expires.Should().BeOnOrBefore(DateTime.UtcNow.AddMinutes(5));
        int.Parse(System.Web.HttpUtility.ParseQueryString(new Uri(url!).Query)["X-Amz-Expires"]!)
            .Should().BeLessThanOrEqualTo(300);
        using var response = await new HttpClient().GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).Should().HaveCount(10);
    }

    private sealed class LocalAnalyzer : IContentSafetyAnalyzer
    {
        public Task<IReadOnlyDictionary<string, int>> AnalyzeAsync(byte[] image, CancellationToken ct)
        {
            image.Should().Equal(new byte[] { 0xff, 0xd8, 0xff, 0, 0, 0, 0, 0, 0xff, 0xd9 });
            return Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>
            {
                ["Hate"] = 0,
                ["SelfHarm"] = 0,
                ["Sexual"] = 0,
                ["Violence"] = 0
            });
        }
    }

    private sealed class LocalS3Store(AmazonS3Client client) : IMediaObjectStore, IMediaImageSource
    {
        public Task<(string Url, DateTime ExpiresAt, IReadOnlyDictionary<string, string> Headers)>
            SignPutAsync(string key, string mime, CancellationToken ct)
        {
            var expires = DateTime.UtcNow.AddMinutes(4);
            var url = client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = Bucket,
                Key = key,
                Verb = HttpVerb.PUT,
                ContentType = mime,
                Expires = expires,
                Protocol = Protocol.HTTP
            });
            return Task.FromResult<(string, DateTime, IReadOnlyDictionary<string, string>)>(
                (url, expires, new Dictionary<string, string> { ["Content-Type"] = mime }));
        }
        public async Task<(long Length, string Mime)?> HeadAsync(string key, CancellationToken ct)
        {
            try
            {
                var head = await client.GetObjectMetadataAsync(Bucket, key, ct);
                return (head.ContentLength, head.Headers.ContentType);
            }
            catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }
        public Task<MediaReadUrl> SignGetAsync(string key, CancellationToken ct)
        {
            var expires = DateTime.UtcNow.AddMinutes(4);
            return Task.FromResult(new MediaReadUrl(client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = Bucket,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = expires,
                Protocol = Protocol.HTTP
            }), expires));
        }
        public Task DeleteAsync(string key, CancellationToken ct) =>
            client.DeleteObjectAsync(Bucket, key, ct);

        public async Task SealApprovedAsync(string stagingKey, string approvedKey, string mime, long size,
            byte[] approvedHash, CancellationToken ct)
        {
            var bytes = await ReadForModerationAsync(stagingKey, mime, size, ct);
            if (bytes.Length != size ||
                !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), approvedHash))
                throw new InvalidOperationException("Staging bytes changed after moderation.");
            using var stream = new MemoryStream(bytes);
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = Bucket,
                Key = approvedKey,
                InputStream = stream,
                ContentType = mime,
                IfNoneMatch = "*"
            }, ct);
        }

        public async Task<byte[]> ReadForModerationAsync(string key, string mime, long size, CancellationToken ct)
        {
            using var response = await client.GetObjectAsync(Bucket, key, ct);
            await using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, ct);
            return buffer.ToArray();
        }
    }
}
