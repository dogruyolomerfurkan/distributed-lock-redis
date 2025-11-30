using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using StackExchange.Redis;
using WebApi.DistributedLockStuff;

namespace WebApi.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class TestController(IDistributedLockFactory lockFactory, ILogger<TestController> logger, HybridCache cache) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> TestConcurrentExecutions(CancellationToken cancellationToken)
        {
            try
            {
                await using var @lock = await lockFactory.AcquireAsync(
                    resource: $"count",
                    expiry: TimeSpan.FromSeconds(30),
                    wait: TimeSpan.FromSeconds(10),
                    retry: TimeSpan.FromMilliseconds(100),
                    ct: cancellationToken);

                if (!@lock.IsAcquired)
                {
                    logger.LogWarning("Failed to acquire lock for count resource within timeout period");
                    return StatusCode(503, new { error = "Service temporarily unavailable. Please try again later." });
                }

                var entryOptions = new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromMinutes(10),
                    LocalCacheExpiration = TimeSpan.FromSeconds(30)
                };

                var result = await cache.GetOrCreateAsync(key: "COUNT",
                    factory: async cancel => 0,
                    options: entryOptions,
                    cancellationToken: cancellationToken);


                var intData = result + 1;
                await cache.SetAsync("COUNT", intData,
                    entryOptions,
                    cancellationToken: cancellationToken);

                //logger.LogInformation("New count is {count}", intData);

                return Ok();
            }
            catch (TaskCanceledException)
            {
                logger.LogWarning("Request cancelled by client");
                return StatusCode(499, new { error = "Client closed request" });
            }
            catch (InvalidOperationException ex) when (ex.InnerException is RedisException)
            {
                logger.LogError(ex, "Redis connection error occurred");
                return StatusCode(503, new { error = "Service temporarily unavailable due to connection issues" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error occurred while processing request");
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }
    }
}
