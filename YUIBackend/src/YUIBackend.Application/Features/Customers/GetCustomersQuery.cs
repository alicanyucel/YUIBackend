using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;

namespace MyProject.Application.Features.Customers;

public sealed record GetCustomersQuery(int PageNumber = 1, int PageSize = 20, Guid? CustomerId = null)
    : IQuery<PagedResult<CustomerDto>>;
