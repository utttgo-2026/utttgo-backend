using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UTTTGO.Api.Modules.Identity.Application;
using UTTTGO.Api.Modules.Media.Application;
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

    [Fact]
    public async Task StatusIncludesPhysicalMediaPurgeState()
    {
        using var factory = StatusFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Account-Deletion-Proof", "01234567890123456789012345678901");

        using var response = await client.GetAsync(
            "/api/v1/users/me/deletion/11111111-1111-1111-1111-111111111111");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("mediaPurge").And.Contain("Completed");
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

    private static WebApplicationFactory<Program> StatusFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDeleteOwnAccountService>();
                services.AddSingleton<IDeleteOwnAccountService>(new Stub(DeleteOwnAccountOutcome.Success));
                services.RemoveAll<IAccountDeletionOperationStore>();
                services.AddSingleton<IAccountDeletionOperationStore>(new StatusOperations());
                services.RemoveAll<IMediaDeletionStatusReader>();
                services.AddSingleton<IMediaDeletionStatusReader>(new StatusMedia());
            }));

    private sealed class Stub(DeleteOwnAccountOutcome outcome) : IDeleteOwnAccountService
    {
        public Task<DeleteOwnAccountResult> DeleteAsync(string token, string operationId, string proof,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DeleteOwnAccountResult(outcome));
    }

    private sealed class StatusOperations : IAccountDeletionOperationStore
    {
        public Task<AccountDeletionOperation?> FindAsync(string operationId, string proofHash,
            CancellationToken cancellationToken) => Task.FromResult<AccountDeletionOperation?>(new(
            operationId, "user-1", "subject-1", null, 1, "deleted-ref", "revocation", 0,
            AccountDeletionOperationStates.Completed, AccountDeletionStages.Completed, null,
            DateTime.UtcNow, DateTime.UtcNow));

        public Task<AccountDeletionOperation?> CreateOrGetAsync(string operationId, string proofHash,
            string subject, string userId, string? sellerId, long expectedVersion, string deletedUserRef,
            string revocationId, long expiration, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AccountDeletionOperation>> ListRecoverableAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AccountDeletionOperation?> MarkAsync(string operationId, string status, string stage,
            string? failureCode, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AccountDeletionOperation?> MarkFailureAsync(string operationId, string status,
            string? failureCode, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StatusMedia : IMediaDeletionStatusReader
    {
        public Task<MediaDeletionStatus?> FindDeletionStatusAsync(string ownerUserId,
            string deletionReference, CancellationToken cancellationToken) => Task.FromResult<MediaDeletionStatus?>(
            new("Completed", 2, DateTime.UtcNow.AddMinutes(-1), null));
    }
}
