using Microsoft.EntityFrameworkCore;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;
using MyProject.Infrastructure.Persistence;

namespace MyProject.Infrastructure.Repositories;

internal sealed class CustomerRepository(AppDbContext context)
    : Repository<Customer>(context), ICustomerRepository
{
    public Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(customer => customer.Email == email, cancellationToken);

    public async Task<PagedResult<Customer>> GetPageByIdAsync(
        Guid customerId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking().Where(customer => customer.Id == customerId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(customer => customer.CreatedAt).ThenBy(customer => customer.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Customer>(items, totalCount, pageNumber, pageSize);
    }
}
