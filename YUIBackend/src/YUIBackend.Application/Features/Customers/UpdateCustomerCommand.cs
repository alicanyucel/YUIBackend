using MyProject.Application.Abstractions.Messaging;

namespace MyProject.Application.Features.Customers;

public sealed record UpdateCustomerCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool? IsActive = null) : ICommand;
