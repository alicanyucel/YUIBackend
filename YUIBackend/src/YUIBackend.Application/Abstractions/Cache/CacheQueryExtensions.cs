using System.Security.Cryptography;
using System.Text.Json;

namespace MyProject.Application.Abstractions.Cache;

public static class CacheQueryExtensions
{
    public static async Task<T> GetOrCreateAsync<T>(
        this ICacheService cache,
        string scope,
        object criteria,
        Func<Task<T>> factory,
        CancellationToken cancellationToken,
        TimeSpan? expiry = null)
        where T : class
    {
        var version = await cache.GetVersionAsync(scope, cancellationToken);
        var criteriaBytes = JsonSerializer.SerializeToUtf8Bytes(criteria);
        var criteriaHash = Convert.ToHexString(SHA256.HashData(criteriaBytes));
        var key = $"api-cache:{scope}:{version}:{criteriaHash}";
        var cached = await cache.GetAsync<T>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await factory();
        await cache.SetAsync(key, value, expiry ?? TimeSpan.FromMinutes(1), cancellationToken);
        return value;
    }
}
