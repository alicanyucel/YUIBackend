using MyProject.Application.Abstractions.Messaging;

namespace MyProject.Application.Features.Customers;

public sealed record DeleteCustomerCommand(Guid Id) : ICommand;
