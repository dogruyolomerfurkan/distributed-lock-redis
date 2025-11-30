using StackExchange.Redis;

namespace WebApi.DistributedLockStuff
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDistributedLock(this IServiceCollection services)
        {
            var redisConfiguration = new ConfigurationOptions
            {
                ConnectRetry = 5,
                ConnectTimeout = 5000,
                AbortOnConnectFail = false,  // Important: allows app to start even if Redis is down
                SyncTimeout = 5000,
                AsyncTimeout = 5000,
                EndPoints = { "localhost:6379" },
                KeepAlive = 60,

                ReconnectRetryPolicy = new ExponentialRetry(5000)
            };

            var sharedRedisConnection = ConnectionMultiplexer.Connect(redisConfiguration);

            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                // Log connection events for monitoring
                sharedRedisConnection.ConnectionFailed += (sender, args) =>
                {
                    sp.GetRequiredService<ILogger<Program>>()
                        .LogError("Redis connection failed: {EndPoint} - {FailureType}",
                            args.EndPoint, args.FailureType);
                };

                sharedRedisConnection.ConnectionRestored += (sender, args) =>
                {
                    sp.GetRequiredService<ILogger<Program>>()
                        .LogInformation("Redis connection restored: {EndPoint}", args.EndPoint);
                };

                return sharedRedisConnection;
            });

            services.AddHybridCache(options =>
            {
                options.MaximumPayloadBytes = 1024 * 1024;
                options.MaximumKeyLength = 512;
                options.DefaultEntryOptions = new Microsoft.Extensions.Caching.Hybrid.HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromMinutes(10),
                    LocalCacheExpiration = TimeSpan.FromMinutes(5)
                };

            });

            services.AddStackExchangeRedisCache(opt =>
            {
                opt.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(sharedRedisConnection);
            });


            services.AddSingleton<IDistributedLockFactory, DistributedLockFactory>();

            return services;
        }
    }
}
