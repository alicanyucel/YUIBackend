using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;

namespace MyProject.Domain.Repositories;

public interface IAccountRepository : IRepository<Account>
{
    Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<Account>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<PagedResult<Account>> GetPageByCustomerIdAsync(Guid customerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
