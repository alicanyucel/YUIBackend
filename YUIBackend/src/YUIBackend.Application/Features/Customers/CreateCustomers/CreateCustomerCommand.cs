using MyProject.Application.Abstractions.Messaging;

namespace YUIBackend.Application.Features.Customers.CreateCustomers;

public sealed record CreateCustomerCommand(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber) : ICommand<Guid>;
