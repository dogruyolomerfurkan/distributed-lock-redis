using StackExchange.Redis;

namespace WebApi.DistributedLockStuff
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDistributedLock(this IServiceCollection services, string connectionString)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(connectionString));

            services.AddSingleton<IDistributedLockFactory, DistributedLockFactory>();

            return services;
        }
    }
}
