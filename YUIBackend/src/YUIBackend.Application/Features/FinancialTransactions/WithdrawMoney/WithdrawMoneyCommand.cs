using MyProject.Application.Abstractions.Messaging;
using YUIBackend.Application.Features.FinancialTransactions.FinancialOperations;

namespace YUIBackend.Application.Features.FinancialTransactions.WithdrawMoney;

public sealed record WithdrawMoneyCommand(
    Guid AccountId,
    decimal Amount,
    string IdempotencyKey,
    string? Description) : ICommand<FinancialOperationResponse>;
