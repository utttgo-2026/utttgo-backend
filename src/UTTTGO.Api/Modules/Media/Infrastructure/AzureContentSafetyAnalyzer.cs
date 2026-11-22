using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Media.Application;

namespace UTTTGO.Api.Modules.Media.Infrastructure;

public sealed class AzureContentSafetyAnalyzer(HttpClient client, IOptions<ContentSafetyOptions> options)
    : IContentSafetyAnalyzer
{
    public async Task<IReadOnlyDictionary<string, int>> AnalyzeAsync(byte[] image,
        CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (!Uri.TryCreate(config.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps || endpoint.AbsolutePath != "/" ||
            endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            endpoint.UserInfo.Length != 0 || string.IsNullOrWhiteSpace(config.SubscriptionKey))
            throw new InvalidOperationException("Content Safety provider is not configured.");
        if (image.Length is <= 0 or > MediaImagePolicy.MaxBytes)
            throw new InvalidOperationException("Invalid moderation image size.");

        var address = new Uri(endpoint, "contentsafety/image:analyze?api-version=2024-09-01");
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var attemptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                return await AnalyzeOnceAsync(address, config.SubscriptionKey, image, attemptTimeout.Token,
                    attempt < 3);
            }
            catch (RetryableContentSafetyException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
            catch (HttpRequestException) when (attempt < 3 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
            catch (OperationCanceledException) when (attempt < 3 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
        }
        throw new InvalidOperationException("Content Safety provider failed.");
    }

    private async Task<IReadOnlyDictionary<string, int>> AnalyzeOnceAsync(Uri address, string subscriptionKey,
        byte[] image, CancellationToken attemptToken, bool canRetry)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, address);
        request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(new
        {
            image = new { content = Convert.ToBase64String(image) },
            categories = new[] { "Hate", "SelfHarm", "Sexual", "Violence" },
            outputType = "FourSeverityLevels"
        }));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            attemptToken);
        if (canRetry && (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
            (int)response.StatusCode >= 500))
            throw new RetryableContentSafetyException();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Content Safety provider failed.");
        await using var stream = await response.Content.ReadAsStreamAsync(attemptToken);
        using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
        {
            MaxDepth = 12
        }, attemptToken);
        if (!document.RootElement.TryGetProperty("categoriesAnalysis", out var categories) ||
            categories.ValueKind != JsonValueKind.Array || categories.GetArrayLength() != 4)
            throw new InvalidOperationException("Unexpected Content Safety response.");
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in categories.EnumerateArray())
        {
            if (!item.TryGetProperty("category", out var name) || name.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("severity", out var value) || !value.TryGetInt32(out var severity) ||
                !result.TryAdd(name.GetString()!, severity))
                throw new InvalidOperationException("Unexpected Content Safety response.");
        }
        return result;
    }

    private sealed class RetryableContentSafetyException : Exception;
}
