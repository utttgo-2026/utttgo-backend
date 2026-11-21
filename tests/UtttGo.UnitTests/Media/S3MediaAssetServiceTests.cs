using FluentAssertions;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Media.Infrastructure;
using UTTTGO.Api.Shared.Contracts;
using Xunit;

namespace UtttGo.UnitTests.Media;

public sealed class S3MediaAssetServiceTests
{
    [Fact]
    public async Task DefaultCredentialChainSignsWithTemporarySessionToken()
    {
        var result = await WithEnvironmentAsync(async () =>
        {
            var service = CreateService(new S3Config { BucketName = "test-bucket" });
            return await service.GetPresignedPutUrlAsync(
                "asset-1", "staging/owner-1/asset-1", "image/jpeg");
        }, useTemporaryCredentials: true);

        result.UploadUrl.Should().Contain("X-Amz-Security-Token=temporary-session-token");
    }

    [Fact]
    public async Task ExplicitCredentialsSignWithoutSessionToken()
    {
        var result = await WithEnvironmentAsync(async () =>
        {
            var service = CreateService(new S3Config
            {
                BucketName = "test-bucket",
                AccessKey = "explicit-access-key",
                SecretKey = "explicit-secret-key"
            });
            return await service.GetPresignedPutUrlAsync(
                "asset-2", "staging/owner-1/asset-2", "image/jpeg");
        }, useTemporaryCredentials: false);

        result.UploadUrl.Should().Contain("X-Amz-Credential=explicit-access-key");
        result.UploadUrl.Should().NotContain("X-Amz-Security-Token");
    }

    [Fact]
    public async Task MissingBucketUsesTheTestMock()
    {
        var result = await CreateService(new S3Config()).GetPresignedPutUrlAsync(
            "asset-3", "staging/owner-1/asset-3", "image/jpeg");

        result.UploadUrl.Should().Contain("mock-s3-bucket");
    }

    private static S3MediaAssetService CreateService(S3Config config) =>
        new(Options.Create(config));

    private static async Task<T> WithEnvironmentAsync<T>(Func<Task<T>> action,
        bool useTemporaryCredentials)
    {
        var previousAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        var previousSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
        var previousSessionToken = Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID",
                useTemporaryCredentials ? "temporary-access-key" : null);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY",
                useTemporaryCredentials ? "temporary-secret-key" : null);
            Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN",
                useTemporaryCredentials ? "temporary-session-token" : null);
            return await action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", previousAccessKey);
            Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", previousSecretKey);
            Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", previousSessionToken);
        }
    }
}
