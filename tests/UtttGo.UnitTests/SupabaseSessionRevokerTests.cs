using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UTTTGO.Api.Modules.Identity.Infrastructure;
using Xunit;

namespace UtttGo.UnitTests;

public sealed class SupabaseSessionRevokerTests
{
    [Fact]
    public async Task SuccessfulLogoutUsesGlobalScopeAndBearerToken()
    {
        HttpRequestMessage? captured = null;
        using var client = new HttpClient(new Handler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));

        await Revoker(client).RevokeGloballyAsync("access-token", default);

        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.ToString().Should().Be(
            "https://example.supabase.co/auth/v1/logout?scope=global");
        captured.Headers.GetValues("apikey").Single().Should().Be("anon-key");
        captured.Headers.Authorization.Should().Be(
            new AuthenticationHeaderValue("Bearer", "access-token"));
    }

    [Fact]
    public async Task ProviderFailureDoesNotEscape()
    {
        using var client = new HttpClient(new Handler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadGateway)));

        var action = () => Revoker(client).RevokeGloballyAsync("access-token", default);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProviderTimeoutDoesNotEscape()
    {
        using var client = new HttpClient(new Handler(_ => throw new TaskCanceledException()));

        var action = () => Revoker(client).RevokeGloballyAsync("access-token", default);

        await action.Should().NotThrowAsync();
    }

    private static SupabaseSessionRevoker Revoker(HttpClient client) =>
        new(new Factory(client), Options.Create(new SupabaseOptions
        {
            Url = "https://example.supabase.co",
            AnonKey = "anon-key"
        }), NullLogger<SupabaseSessionRevoker>.Instance);

    private sealed class Factory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(send(request));
    }
}
