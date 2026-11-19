using Microsoft.EntityFrameworkCore;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;
using MyProject.Infrastructure.Persistence;

namespace MyProject.Infrastructure.Repositories;

internal sealed class FinancialTransactionRepository(AppDbContext context)
    : Repository<FinancialTransaction>(context), IFinancialTransactionRepository
{
    public Task<FinancialTransaction?> GetByTransactionNumberAsync(
        string transactionNumber,
        CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(
            transaction => transaction.TransactionNumber == transactionNumber,
            cancellationToken);

    public async Task<IEnumerable<FinancialTransaction>> GetByAccountIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
        => await DbSet.Where(transaction =>
                transaction.SourceAccountId == accountId || transaction.DestinationAccountId == accountId)
            .OrderByDescending(transaction => transaction.OccurredAt)
            .ToListAsync(cancellationToken);

    public override async Task<PagedResult<FinancialTransaction>> GetPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .OrderByDescending(transaction => transaction.OccurredAt)
            .ThenByDescending(transaction => transaction.Id);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<FinancialTransaction>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResult<FinancialTransaction>> GetPageByAccountIdAsync(
        Guid accountId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .Where(transaction => transaction.SourceAccountId == accountId || transaction.DestinationAccountId == accountId)
            .OrderByDescending(transaction => transaction.OccurredAt)
            .ThenByDescending(transaction => transaction.Id);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<FinancialTransaction>(items, totalCount, pageNumber, pageSize);
    }
}
