using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;

namespace MyProject.Domain.Repositories;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<PagedResult<Customer>> GetPageByIdAsync(Guid customerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
