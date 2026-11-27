using UTTTGO.Api.Modules.Identity.Application;

namespace UtttGo.IntegrationTests;

internal sealed class AccountDeletionFenceDouble : IAccountDeletionFence
{
    public Task<IAsyncDisposable?> AcquireAsync(IEnumerable<string> accountKeys,
        CancellationToken cancellationToken) =>
        Task.FromResult<IAsyncDisposable?>(new Lease());

    private sealed class Lease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
