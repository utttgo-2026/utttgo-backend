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
        if (image.Length is <= 0 or > 5_242_880)
            throw new InvalidOperationException("Invalid moderation image size.");

        var address = new Uri(endpoint, "contentsafety/image:analyze?api-version=2024-09-01");
        using var request = new HttpRequestMessage(HttpMethod.Post, address);
        request.Headers.Add("Ocp-Apim-Subscription-Key", config.SubscriptionKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(new
        {
            image = new { content = Convert.ToBase64String(image) },
            categories = new[] { "Hate", "SelfHarm", "Sexual", "Violence" },
            outputType = "FourSeverityLevels"
        }));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Content Safety provider failed.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
        {
            MaxDepth = 12
        }, cancellationToken);
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
}
