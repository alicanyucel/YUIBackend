using MyProject.Application.Abstractions.Messaging;

namespace MyProject.Application.Features.Accounts;

public sealed record UpdateAccountCommand(Guid Id, string Status) : ICommand;
