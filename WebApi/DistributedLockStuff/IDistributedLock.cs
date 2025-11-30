namespace WebApi.DistributedLockStuff;

public interface IDistributedLock : IAsyncDisposable
{
    bool IsAcquired { get; }
    string Resource { get; }
}
