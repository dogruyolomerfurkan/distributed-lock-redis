using StackExchange.Redis;

namespace WebApi.DistributedLockStuff;

public sealed class DistributedLockFactory(
    IConnectionMultiplexer connection,
    ILogger<DistributedLock> lockLogger) : IDistributedLockFactory
{
    public async Task<IDistributedLock> AcquireAsync(
        string resource,
        TimeSpan expiry,
        TimeSpan? wait = null,
        TimeSpan? retry = null,
        CancellationToken ct = default)
    {
        var db = connection.GetDatabase();
        return await DistributedLock.AcquireAsync(db, resource, expiry, lockLogger, wait, retry, ct);
    }
}
