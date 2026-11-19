namespace MyProject.Application.Abstractions.Cache;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<string> GetVersionAsync(string scope, CancellationToken cancellationToken = default);
    Task InvalidateAsync(string scope, CancellationToken cancellationToken = default);
}
