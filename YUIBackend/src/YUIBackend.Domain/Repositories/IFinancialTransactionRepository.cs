using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;

namespace MyProject.Domain.Repositories;

public interface IFinancialTransactionRepository : IRepository<FinancialTransaction>
{
    Task<FinancialTransaction?> GetByTransactionNumberAsync(string transactionNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<FinancialTransaction>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<PagedResult<FinancialTransaction>> GetFilteredPageAsync(
        FinancialTransactionFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<FinancialReport> GetReportAsync(
        FinancialReportFilter filter,
        FinancialReportPeriodKind periodKind,
        CancellationToken cancellationToken = default);
    Task<FinancialTransaction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
