using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using Xunit;

namespace UtttGo.SecurityTests;

public sealed class DeleteOwnAccountSecurityTests
{
    [Fact]
    public async Task MissingBearerIsRejectedBeforeDeletionService()
    {
        using var factory = Factory(DeleteOwnAccountOutcome.Success);

        using var response = await factory.CreateClient().DeleteAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNAUTHORIZED");
    }

    [Fact]
    public async Task RevokedOrInvalidSessionIsNotReportedAsSuccess()
    {
        using var factory = Factory(DeleteOwnAccountOutcome.Unauthorized);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "revoked-token");
        client.DefaultRequestHeaders.Add("X-Account-Deletion-Id", "11111111-1111-1111-1111-111111111111");
        client.DefaultRequestHeaders.Add("X-Account-Deletion-Proof", "01234567890123456789012345678901");

        using var response = await client.DeleteAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FailureToRevokeIsFailClosed()
    {
        using var factory = Factory(DeleteOwnAccountOutcome.Unavailable);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "valid-token");
        client.DefaultRequestHeaders.Add("X-Account-Deletion-Id", "11111111-1111-1111-1111-111111111111");
        client.DefaultRequestHeaders.Add("X-Account-Deletion-Proof", "01234567890123456789012345678901");

        using var response = await client.DeleteAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
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
