using FluentAssertions;
using MyProject.Application.Abstractions.Cache;

namespace MyProject.Tests.Application.Cache;

public sealed class CacheQueryExtensionsTests
{
    [Fact]
    public async Task GetOrCreateAsync_ReusesValueUntilItsScopeIsInvalidated()
    {
        var cache = new InMemoryCacheService();
        var factoryCalls = 0;
        var criteria = new { Page = 1, TenantId = "tenant-a" };

        var first = await cache.GetOrCreateAsync("products", criteria, () =>
        {
            factoryCalls++;
            return Task.FromResult(new[] { "product-a" });
        }, CancellationToken.None);
        var cached = await cache.GetOrCreateAsync("products", criteria, () =>
        {
            factoryCalls++;
            return Task.FromResult(new[] { "unexpected" });
        }, CancellationToken.None);

        cached.Should().Equal(first);
        factoryCalls.Should().Be(1);

        await cache.InvalidateAsync("products");
        var refreshed = await cache.GetOrCreateAsync("products", criteria, () =>
        {
            factoryCalls++;
            return Task.FromResult(new[] { "product-b" });
        }, CancellationToken.None);

        refreshed.Should().Equal("product-b");
        factoryCalls.Should().Be(2);
    }

    private sealed class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _values = [];
        private readonly Dictionary<string, int> _versions = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_values.TryGetValue(key, out var value) ? (T)value : default);

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
        {
            _values[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }

        public Task<string> GetVersionAsync(string scope, CancellationToken cancellationToken = default)
            => Task.FromResult(_versions.TryGetValue(scope, out var version) ? version.ToString() : "0");

        public Task InvalidateAsync(string scope, CancellationToken cancellationToken = default)
        {
            _versions[scope] = _versions.GetValueOrDefault(scope) + 1;
            return Task.CompletedTask;
        }
    }
}
