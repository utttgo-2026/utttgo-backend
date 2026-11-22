using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Identity.Application;

namespace UTTTGO.Api.Modules.Identity.Infrastructure;

public sealed class SupabaseSessionRevoker(
    IHttpClientFactory httpClientFactory,
    IOptions<SupabaseOptions> settings,
    ILogger<SupabaseSessionRevoker> logger) : ISupabaseSessionRevoker
{
    public async Task RevokeGloballyAsync(string accessToken, CancellationToken cancellationToken)
    {
        var options = settings.Value;
        if (string.IsNullOrWhiteSpace(options.Url) || string.IsNullOrWhiteSpace(options.AnonKey) ||
            !Uri.TryCreate(options.Url, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment))
        {
            Warn("configuration");
            return;
        }

        var endpoint = new Uri(baseUri.ToString().TrimEnd('/') + "/auth/v1/logout?scope=global");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.TryAddWithoutValidation("apikey", options.AnonKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClientFactory.CreateClient("SupabaseLogout")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) Warn("http_status");
        }
        catch (TaskCanceledException)
        {
            Warn("timeout");
        }
        catch (HttpRequestException)
        {
            Warn("transport");
        }
        catch (Exception)
        {
            Warn("unexpected");
        }
    }

    private void Warn(string errorType) =>
        logger.LogWarning("Supabase global logout failed with error type {ErrorType}.", errorType);
}
