using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using YUIBackend.Application.Features.DTOS;

namespace YUIBackend.Application.Features.Customers.GetCustomers;

public sealed record GetCustomersQuery(int PageNumber = 1, int PageSize = 20, Guid? CustomerId = null)
    : IQuery<PagedResult<CustomerDto>>;
