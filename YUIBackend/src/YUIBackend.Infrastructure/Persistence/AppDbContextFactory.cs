using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Application.Abstractions.MultiTenancy;
using MyProject.Domain.Entities;

namespace MyProject.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var searchDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
        string? webApiDirectory = null;

        while (searchDirectory is not null)
        {
            var candidate = Path.Combine(searchDirectory.FullName, "YUIBackend", "src", "YUIBackend.WebAPI");
            if (File.Exists(Path.Combine(candidate, "YUIBackend.WebAPI.csproj")))
            {
                webApiDirectory = candidate;
                break;
            }

            candidate = Path.Combine(searchDirectory.FullName, "src", "YUIBackend.WebAPI");
            if (File.Exists(Path.Combine(candidate, "YUIBackend.WebAPI.csproj")))
            {
                webApiDirectory = candidate;
                break;
            }

            searchDirectory = searchDirectory.Parent;
        }

        if (webApiDirectory is null)
        {
            throw new InvalidOperationException("YUIBackend.WebAPI project directory could not be found.");
        }

        var config = new ConfigurationBuilder()
            .AddUserSecrets("ab477883-460d-44ca-8025-d5c20255f6a4")
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:SqlServer must be configured using User Secrets or environment variables.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        // Design-time için stub implementasyonlar
        var publisher = new NoOpPublisher();
        var eventBus = new NoOpEventBus();
        var tenantService = new NoOpTenantService();

        return new AppDbContext(optionsBuilder.Options, publisher, () => eventBus, tenantService);
    }
}

// Design-time stub — çalışma zamanında gerçek implementasyonlar inject edilir
file sealed class NoOpPublisher : IPublisher
{
    public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification => Task.CompletedTask;
}

file sealed class NoOpEventBus : IEventBus
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        => Task.CompletedTask;

    public Task PublishAsync(object message, Type messageType, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

file sealed class NoOpTenantService : ITenantService
{
    public string? GetCurrentTenantId() => null;
}
