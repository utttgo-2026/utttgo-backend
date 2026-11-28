using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using Testcontainers.MongoDb;
using Testcontainers.Redis;

namespace UtttGo.LocalStaging;

public static class StagingProgram
{
    public static async Task Main()
    {
        const string database = "utttgo_local_staging";
        const string bucket = "utttgo-local-staging";
        const string url = "http://0.0.0.0:5080";

        await using var mongo = new MongoDbBuilder("mongo:7")
            .WithLabel("utttgo.local-staging", "true")
            .Build();
        await using var redis = new RedisBuilder("redis:7-alpine")
            .WithLabel("utttgo.local-staging", "true")
            .Build();
        await using var localstack = new ContainerBuilder("localstack/localstack:4.9")
            .WithLabel("utttgo.local-staging", "true")
            .WithEnvironment("SERVICES", "s3")
            .WithPortBinding(4566, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                request => request.ForPort(4566).ForPath("/_localstack/health")))
            .Build();

        await mongo.StartAsync();
        await redis.StartAsync();
        await localstack.StartAsync();

        var redisUrl = $"redis://{redis.Hostname}:{redis.GetMappedPublicPort(6379)}";
        var s3Url = $"http://{localstack.Hostname}:{localstack.GetMappedPublicPort(4566)}";
        using var s3 = new AmazonS3Client(new Amazon.Runtime.BasicAWSCredentials("local-test", "local-test"),
            new AmazonS3Config { ServiceURL = s3Url, ForcePathStyle = true });
        await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket });

        var apiDll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/UTTTGO.Api/bin/Release/net10.0/UTTTGO.Api.dll"));
        var api = StartApi(apiDll, mongo.GetConnectionString(), database, redisUrl, bucket, s3Url, url);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopped.TrySetResult();
        };

        Console.WriteLine($"Local staging listening at {url}");
        Console.WriteLine("Android emulator base URL: http://10.0.2.2:5080");
        Console.WriteLine("Auth: local HMAC JWT double; not Supabase and not production-compatible.");
        Console.WriteLine("Dependencies: ephemeral MongoDB, Redis and LocalStack S3 containers.");
        Console.WriteLine("Press Ctrl+C to stop and remove all containers.");

        try
        {
            await Task.WhenAny(api.WaitForExitAsync(), stopped.Task);
        }
        finally
        {
            if (!api.HasExited)
                api.Kill(entireProcessTree: true);
            await api.WaitForExitAsync();
        }
    }

    private static Process StartApi(string apiDll, string mongoUri, string database, string redisUrl,
        string bucket, string s3Url, string url)
    {
        var startInfo = new ProcessStartInfo("dotnet", $"\"{apiDll}\"")
        {
            WorkingDirectory = Path.GetDirectoryName(apiDll)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "LocalStaging";
        startInfo.Environment["ASPNETCORE_URLS"] = url;
        startInfo.Environment["LOCAL_STAGING_ENABLED"] = "true";
        startInfo.Environment["MONGODB_URI"] = mongoUri;
        startInfo.Environment["MONGODB_DATABASE"] = database;
        startInfo.Environment["REDIS_URL"] = redisUrl;
        startInfo.Environment["S3_BUCKET_IMAGES"] = bucket;
        startInfo.Environment["S3_SERVICE_URL"] = s3Url;
        startInfo.Environment["AWS_REGION"] = "us-east-2";
        startInfo.Environment["AWS_ACCESS_KEY_ID"] = "local-test";
        startInfo.Environment["AWS_SECRET_ACCESS_KEY"] = "local-test";
        startInfo.Environment["CATALOG_SEED_ENABLED"] = "true";

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start local API.");
        process.OutputDataReceived += (_, args) => { if (args.Data is not null) Console.WriteLine(args.Data); };
        process.ErrorDataReceived += (_, args) => { if (args.Data is not null) Console.Error.WriteLine(args.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }
}
