using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.IntegrationTests;

public sealed class DeleteOwnAccountEndpointTests
{
    [Fact]
    public async Task SuccessfulDeletionReturnsNoContent()
    {
        using var factory = Factory(DeleteOwnAccountOutcome.Success);
        using var client = AuthorizedClient(factory);

        using var response = await client.DeleteAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ActiveOrderReturnsConflictContract()
    {
        using var factory = Factory(DeleteOwnAccountOutcome.Conflict);
        using var client = AuthorizedClient(factory);

        using var response = await client.DeleteAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ACTIVE_ORDER_CONFLICT");
    }

    [Fact]
    public async Task DependencyFailureReturnsUnavailableContract()
    {
        using var factory = Factory(DeleteOwnAccountOutcome.Unavailable);
        using var client = AuthorizedClient(factory);

        using var response = await client.DeleteAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ACCOUNT_DELETION_UNAVAILABLE");
    }

    private static HttpClient AuthorizedClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        client.DefaultRequestHeaders.Add("X-Account-Deletion-Id", "11111111-1111-1111-1111-111111111111");
        client.DefaultRequestHeaders.Add("X-Account-Deletion-Proof", "01234567890123456789012345678901");
        return client;
    }

    private static WebApplicationFactory<Program> Factory(DeleteOwnAccountOutcome outcome) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDeleteOwnAccountService>();
                services.AddSingleton<IDeleteOwnAccountService>(new Stub(outcome));
            }));

    private sealed class Stub(DeleteOwnAccountOutcome outcome) : IDeleteOwnAccountService
    {
        public Task<DeleteOwnAccountResult> DeleteAsync(string token, string operationId, string proof,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DeleteOwnAccountResult(outcome));
    }
}
