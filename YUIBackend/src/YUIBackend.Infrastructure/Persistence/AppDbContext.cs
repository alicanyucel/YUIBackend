using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Application.Abstractions.MultiTenancy;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;

namespace MyProject.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IPublisher publisher,
    IEventBus eventBus,
    ITenantService tenantService) : DbContext(options), IUnitOfWork
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        // Multi-tenant global query filter
        var tenantId = tenantService.GetCurrentTenantId();
        if (!string.IsNullOrEmpty(tenantId))
        {
            modelBuilder.Entity<Product>().HasQueryFilter(p => p.TenantId == tenantId);
            modelBuilder.Entity<Customer>().HasQueryFilter(customer => customer.TenantId == tenantId);
            modelBuilder.Entity<Account>().HasQueryFilter(account => account.TenantId == tenantId);
            modelBuilder.Entity<FinancialTransaction>().HasQueryFilter(transaction => transaction.TenantId == tenantId);
        }

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithEvents = ChangeTracker.Entries<Entity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .Select(entity => (Entity: entity, Events: entity.DomainEvents.ToList()))
            .ToList();

        var domainEvents = entitiesWithEvents.SelectMany(item => item.Events).ToList();

        foreach (var domainEvent in domainEvents)
            await eventBus.PublishAsync(domainEvent, domainEvent.GetType(), cancellationToken);

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var item in entitiesWithEvents)
            item.Entity.ClearDomainEvents();

        foreach (var domainEvent in domainEvents)
            await publisher.Publish(domainEvent, cancellationToken);

        return result;
    }
}
