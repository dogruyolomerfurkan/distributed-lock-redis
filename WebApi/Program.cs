using WebApi.DistributedLockStuff;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 1024 * 1024;
    options.MaximumKeyLength = 512;
    options.DefaultEntryOptions = new Microsoft.Extensions.Caching.Hybrid.HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

});

builder.Services.AddStackExchangeRedisCache(opt =>
{
    opt.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions
    {
        ConnectRetry = 3,
        ConnectTimeout = 5000,
        AbortOnConnectFail = false,
        SyncTimeout = 5000,
        AsyncTimeout = 5000,
        EndPoints = { "localhost:6379" },
        KeepAlive = 60
    };
});
builder.Services.AddDistributedLock("localhost:6379");
var app = builder.Build();

app.MapOpenApi();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
