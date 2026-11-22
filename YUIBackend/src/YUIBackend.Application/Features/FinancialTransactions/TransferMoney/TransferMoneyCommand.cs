using MyProject.Application.Abstractions.Messaging;
using YUIBackend.Application.Features.FinancialTransactions.FinancialOperations;

namespace YUIBackend.Application.Features.FinancialTransactions.TransferMoney;

public sealed record TransferMoneyCommand(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string IdempotencyKey,
    string? Description) : ICommand<FinancialOperationResponse>;
