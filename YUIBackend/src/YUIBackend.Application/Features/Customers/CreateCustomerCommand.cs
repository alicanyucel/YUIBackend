using MyProject.Application.Abstractions.Messaging;

namespace MyProject.Application.Features.Customers;

public sealed record CreateCustomerCommand(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber) : ICommand<Guid>;
