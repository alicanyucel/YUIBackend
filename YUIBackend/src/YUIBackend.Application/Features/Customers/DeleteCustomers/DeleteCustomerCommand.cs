using MyProject.Application.Abstractions.Messaging;

namespace YUIBackend.Application.Features.Customers.DeleteCustomers;

public sealed record DeleteCustomerCommand(Guid Id) : ICommand;
