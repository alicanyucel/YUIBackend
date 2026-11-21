using MyProject.Application.Abstractions.Messaging;

namespace MyProject.Application.Features.Accounts;

public sealed record CreateAccountCommand(
    Guid CustomerId,
    string AccountNumber,
    string CurrencyCode,
    decimal Balance = 0) : ICommand<AccountDto>;
