using MyProject.Application.Abstractions.Messaging;

namespace MyProject.Application.Features.Customers;

public sealed record GetCustomerByIdQuery(Guid Id) : IQuery<CustomerDto>;
