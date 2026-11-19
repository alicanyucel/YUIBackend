using Microsoft.EntityFrameworkCore;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;
using MyProject.Infrastructure.Persistence;

namespace MyProject.Infrastructure.Repositories;

internal sealed class AccountRepository(AppDbContext context)
    : Repository<Account>(context), IAccountRepository
{
    public Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(account => account.AccountNumber == accountNumber, cancellationToken);

    public async Task<IEnumerable<Account>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
        => await DbSet.Where(account => account.CustomerId == customerId)
            .ToListAsync(cancellationToken);
}
