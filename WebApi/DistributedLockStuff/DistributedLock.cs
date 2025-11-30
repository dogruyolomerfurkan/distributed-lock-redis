using StackExchange.Redis;
using System.Diagnostics;

namespace WebApi.DistributedLockStuff;

public class DistributedLock : IDistributedLock
{
    private readonly IDatabase _database;
    private readonly string _lockValue;
    private readonly ILogger<DistributedLock> _logger;
    private bool _disposed;

    //Atomic operation only run in master node with an PRECISE ORDER
    private const string LUA_SCRIPT =
        @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end
        ";

    private DistributedLock(
        ILogger<DistributedLock> logger,
        IDatabase database,
        string resource,
        string lockValue,
        bool acquired)
    {
        _database = database;
        Resource = resource;
        _lockValue = lockValue;
        IsAcquired = acquired;
        _logger = logger;
    }
    public static async Task<DistributedLock> AcquireAsync(
    IDatabase db,
    string resource,
    TimeSpan expiry,
    ILogger<DistributedLock> logger,
    TimeSpan? wait = null,
    TimeSpan? retry = null,
    CancellationToken ct = default)
    {
        var lockKey = $"lock:{resource}";
        var lockValue = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        var waitTime = wait ?? TimeSpan.FromSeconds(30);
        var retryInterval = retry ?? TimeSpan.FromMilliseconds(200);
        var stopwatch = Stopwatch.StartNew();
        var attemptCount = 0;

        while (!ct.IsCancellationRequested)
        {
            attemptCount++;
            try
            {
                //SET NX atomic too!!
                var acquired = await db.StringSetAsync(
                    lockKey,
                    lockValue,
                    expiry,
                    When.NotExists,
                    CommandFlags.DemandMaster);

                if (acquired)
                    return new DistributedLock(logger, db, lockKey, lockValue, true);

                //Didn't obtain lock in defined interval, unavailable lock
                if (stopwatch.Elapsed >= waitTime)
                    return new DistributedLock(logger, db, lockKey, lockValue, false);


                var backoffTime = CalculateBackoff(attemptCount, retryInterval);
                var remainingTime = waitTime - stopwatch.Elapsed;
                var delayTime = backoffTime > remainingTime ? remainingTime : backoffTime;

                await Task.Delay(delayTime, ct);
            }
            catch (RedisException ex)
            {
                logger.LogError(ex, "Redis error while acquiring lock for resource: {Resource}. Attempt {Attempts}",
                    resource, attemptCount);

                // For Redis errors, wait before retrying
                if (stopwatch.Elapsed >= waitTime)
                {
                    throw new InvalidOperationException($"Failed to acquire lock for resource '{resource}' due to Redis errors", ex);
                }

                await Task.Delay(retryInterval, ct);
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while acquiring lock for resource: {Resource}", resource);
                throw;
            }
        }

        //Task canceled
        return new DistributedLock(logger, db, lockKey, lockValue, false);

    }
    private static TimeSpan CalculateBackoff(int attemptCount, TimeSpan baseInterval)
    {
        var exponentialMs = Math.Min(
            baseInterval.TotalMilliseconds * Math.Pow(2, attemptCount - 1),
            5000);

        // Add jitter (random 0-25% of the backoff time) to prevent thundering herd
        var jitter = Random.Shared.NextDouble() * 0.25 * exponentialMs;

        return TimeSpan.FromMilliseconds(exponentialMs + jitter);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed || !IsAcquired)
        {
            GC.SuppressFinalize(this);
            return;
        }

        _disposed = true;

        try
        {
            var result = await _database.ScriptEvaluateAsync(LUA_SCRIPT, [Resource], [_lockValue]);

        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error occurred while releasing lock for resource: {Resource}", Resource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while releasing lock for resource: {Resource}", Resource);
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }


    public bool IsAcquired { get; }
    public string Resource { get; }
}
