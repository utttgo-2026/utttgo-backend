using UTTTGO.Api.Modules.Identity.Application;

namespace UtttGo.UnitTests;

internal sealed class AccountDeletionFenceDouble : IAccountDeletionFence
{
    private readonly Action? acquired;

    public AccountDeletionFenceDouble(Action? acquired = null) => this.acquired = acquired;

    public Task<IAsyncDisposable?> AcquireAsync(IEnumerable<string> accountKeys,
        CancellationToken cancellationToken) =>
        Task.FromResult<IAsyncDisposable?>(Acquire());

    private IAsyncDisposable Acquire()
    {
        acquired?.Invoke();
        return new Lease();
    }

    private sealed class Lease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
