using MyProject.Application.Abstractions.Messaging;
using YUIBackend.Application.Features.DTOS;

namespace YUIBackend.Application.Features.Customers.GetCustomerById;

public sealed record GetCustomerByIdQuery(Guid Id) : IQuery<CustomerDto>;
