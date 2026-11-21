using MyProject.Application.Abstractions.Cache;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Application.Abstractions.MultiTenancy;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Customers;

internal sealed class GetCustomersQueryHandler(
    ICustomerRepository repository,
    ICacheService cache,
    ITenantService tenantService) : IQueryHandler<GetCustomersQuery, PagedResult<CustomerDto>>
{
    public async Task<Result<PagedResult<CustomerDto>>> Handle(
        GetCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantService.GetCurrentTenantId();
        var result = await cache.GetOrCreateAsync("customers", new { request, tenantId }, async () =>
        {
            var page = request.CustomerId is Guid customerId
                ? await repository.GetPageByIdAsync(customerId, request.PageNumber, request.PageSize, cancellationToken)
                : await repository.GetPageAsync(request.PageNumber, request.PageSize, cancellationToken);
            return new PagedResult<CustomerDto>(page.Items.Select(ToDto).ToList(),
                page.TotalCount, page.PageNumber, page.PageSize);
        }, cancellationToken);
        return Result.Success(result);
    }

    internal static CustomerDto ToDto(Customer customer) => new(
        customer.Id, customer.FirstName, customer.LastName, customer.Email,
        customer.PhoneNumber, customer.IsActive, customer.CreatedAt);
}
