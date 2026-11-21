using MyProject.Application.Abstractions.Messaging;

namespace MyProject.Application.Features.FinancialTransactions;

public sealed record TransferMoneyCommand(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string IdempotencyKey,
    string? Description) : ICommand<FinancialOperationResponse>;
