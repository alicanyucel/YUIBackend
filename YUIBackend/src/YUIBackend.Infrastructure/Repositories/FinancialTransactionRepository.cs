using Microsoft.EntityFrameworkCore;
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
}
