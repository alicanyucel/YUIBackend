using MyProject.Application.Abstractions.Cache;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Application.Abstractions.MultiTenancy;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;
using YUIBackend.Application.Features.Customers.GetCustomers;
using YUIBackend.Application.Features.DTOS;

namespace YUIBackend.Application.Features.Customers.GetCustomerById;

internal sealed class GetCustomerByIdQueryHandler(
    ICustomerRepository repository,
    ICacheService cache,
    ITenantService tenantService) : IQueryHandler<GetCustomerByIdQuery, CustomerDto>
{
    public async Task<Result<CustomerDto>> Handle(
        GetCustomerByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantService.GetCurrentTenantId();
        var page = await cache.GetOrCreateAsync("customers", new { request.Id, tenantId }, async () =>
        {
            var customer = await repository.GetByIdAsync(request.Id, cancellationToken);
            var items = customer is null
                ? Array.Empty<CustomerDto>()
                : [GetCustomersQueryHandler.ToDto(customer)];
            return new PagedResult<CustomerDto>(items, items.Length, 1, 1);
        }, cancellationToken);
        return page.Items.Count == 0
            ? Result.Failure<CustomerDto>(Error.NotFound(nameof(Customer)))
            : Result.Success(page.Items[0]);
    }
}
