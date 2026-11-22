using MyProject.Application.Abstractions.Messaging;
using YUIBackend.Application.Features.FinancialTransactions.FinancialOperations;

namespace YUIBackend.Application.Features.FinancialTransactions.DepositMoney;

public sealed record DepositMoneyCommand(
    Guid AccountId,
    decimal Amount,
    string IdempotencyKey,
    string? Description) : ICommand<FinancialOperationResponse>;
