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
    public DbSet<InitialUser> InitialUsers => Set<InitialUser>();

    private string? CurrentTenantId => tenantService.GetCurrentTenantId();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.Entity<Product>().Property(product => product.IsDeleted).HasDefaultValue(false);
        modelBuilder.Entity<Customer>().Property(customer => customer.IsDeleted).HasDefaultValue(false);
        modelBuilder.Entity<Account>().Property(account => account.IsDeleted).HasDefaultValue(false);
        modelBuilder.Entity<FinancialTransaction>().Property(transaction => transaction.IsDeleted).HasDefaultValue(false);
        modelBuilder.Entity<InitialUser>().Property(user => user.IsDeleted).HasDefaultValue(false);
        modelBuilder.Entity<InitialUser>().HasQueryFilter(user => !user.IsDeleted);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<Product>().HasQueryFilter(product =>
            !product.IsDeleted && (CurrentTenantId == null || product.TenantId == CurrentTenantId));
        modelBuilder.Entity<Customer>().HasQueryFilter(customer =>
            !customer.IsDeleted && (CurrentTenantId == null || customer.TenantId == CurrentTenantId));
        modelBuilder.Entity<Account>().HasQueryFilter(account =>
            !account.IsDeleted && (CurrentTenantId == null || account.TenantId == CurrentTenantId));
        modelBuilder.Entity<FinancialTransaction>().HasQueryFilter(transaction =>
            !transaction.IsDeleted && (CurrentTenantId == null || transaction.TenantId == CurrentTenantId));

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
