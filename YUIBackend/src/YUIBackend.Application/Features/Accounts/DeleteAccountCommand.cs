using MyProject.Application.Abstractions.Messaging;

namespace MyProject.Application.Features.Accounts;

public sealed record DeleteAccountCommand(Guid Id) : ICommand;
