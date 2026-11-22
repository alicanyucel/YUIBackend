using MyProject.Application.Abstractions.Messaging;

namespace YUIBackend.Application.Features.Accounts.DeleteAccount;

public sealed record DeleteAccountCommand(Guid Id) : ICommand;
