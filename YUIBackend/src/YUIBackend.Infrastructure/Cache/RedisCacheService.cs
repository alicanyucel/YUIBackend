using Microsoft.Extensions.Logging;
using MyProject.Application.Abstractions.Cache;
using StackExchange.Redis;
using System.Text.Json;

namespace MyProject.Infrastructure.Cache;

internal sealed class RedisCacheService : ICacheService
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMilliseconds(500);
    private readonly IDatabase _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _logger = logger;
        redis.ConnectionFailed += (_, args) =>
            _logger.LogWarning(args.Exception, "Redis connection to {EndPoint} failed; cache operations will be bypassed.", args.EndPoint);
        redis.ConnectionRestored += (_, args) =>
            _logger.LogInformation("Redis connection to {EndPoint} was restored.", args.EndPoint);

        if (!redis.IsConnected)
            _logger.LogWarning("Redis is currently unavailable; cache operations will be bypassed.");
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var value = await ExecuteAsync("read", () => _db.StringGetAsync(key), RedisValue.Null, cancellationToken);
        return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(value.ToString());
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);
        await ExecuteAsync("write", () => _db.StringSetAsync(key, json, expiry ?? TimeSpan.FromMinutes(10)), false, cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => await ExecuteAsync("delete", () => _db.KeyDeleteAsync(key), false, cancellationToken);

    public async Task<string> GetVersionAsync(string scope, CancellationToken cancellationToken = default)
    {
        var version = await ExecuteAsync("read version", () => _db.StringGetAsync($"api-cache-version:{scope}"), RedisValue.Null, cancellationToken);
        return version.IsNullOrEmpty ? "0" : version.ToString();
    }

    public async Task InvalidateAsync(string scope, CancellationToken cancellationToken = default)
        => await ExecuteAsync("invalidate", () => _db.StringIncrementAsync($"api-cache-version:{scope}"), 0L, cancellationToken);

    private async Task<T> ExecuteAsync<T>(
        string operation,
        Func<Task<T>> action,
        T fallback,
        CancellationToken cancellationToken)
    {
        if (!_redis.IsConnected)
            return fallback;

        try
        {
            return await action().WaitAsync(OperationTimeout, cancellationToken);
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(exception, "Redis cache {Operation} failed; continuing without cache.", operation);
            return fallback;
        }
        catch (TimeoutException exception)
        {
            _logger.LogWarning(exception, "Redis cache {Operation} timed out; continuing without cache.", operation);
            return fallback;
        }
    }
}
