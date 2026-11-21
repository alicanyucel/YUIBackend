using MyProject.Application.Abstractions.Messaging;

namespace MyProject.Application.Features.FinancialTransactions;

public sealed record DepositMoneyCommand(
    Guid AccountId,
    decimal Amount,
    string IdempotencyKey,
    string? Description) : ICommand<FinancialOperationResponse>;
