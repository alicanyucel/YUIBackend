using MyProject.Application.Abstractions.Messaging;

namespace YUIBackend.Application.Features.Accounts.UpdateAccount;

public sealed record UpdateAccountCommand(Guid Id, string Status) : ICommand;
