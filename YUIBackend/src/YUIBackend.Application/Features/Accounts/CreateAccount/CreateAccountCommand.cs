using MyProject.Application.Abstractions.Messaging;

namespace YUIBackend.Application.Features.Accounts.CreateAccount;

public sealed record CreateAccountCommand(
    Guid CustomerId,
    string AccountNumber,
    string CurrencyCode,
    decimal Balance = 0) : ICommand<AccountDto>;
