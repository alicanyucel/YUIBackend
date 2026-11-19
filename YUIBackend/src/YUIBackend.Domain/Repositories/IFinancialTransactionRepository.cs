using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;

namespace MyProject.Domain.Repositories;

public interface IFinancialTransactionRepository : IRepository<FinancialTransaction>
{
    Task<FinancialTransaction?> GetByTransactionNumberAsync(string transactionNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<FinancialTransaction>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<PagedResult<FinancialTransaction>> GetPageByAccountIdAsync(
        Guid accountId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
