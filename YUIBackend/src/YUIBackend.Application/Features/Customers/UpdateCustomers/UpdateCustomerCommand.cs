using MyProject.Application.Abstractions.Messaging;

namespace YUIBackend.Application.Features.Customers.UpdateCustomers;

public sealed record UpdateCustomerCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool? IsActive = null) : ICommand;
