namespace WebApi.DistributedLockStuff;

public interface IDistributedLockFactory
{
    Task<IDistributedLock> AcquireAsync(
        string resource,
        TimeSpan expiry,
        TimeSpan? wait = null,
        TimeSpan? retry = null,
        CancellationToken ct = default);

}
