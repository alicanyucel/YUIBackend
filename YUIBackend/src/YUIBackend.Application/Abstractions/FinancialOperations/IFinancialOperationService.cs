using MyProject.Domain.Entities;

namespace MyProject.Application.Abstractions.FinancialOperations;

public sealed record FinancialOperationRequest(
    FinancialTransactionType Type,
    decimal Amount,
    Guid? SourceAccountId,
    Guid? DestinationAccountId,
    string IdempotencyKey,
    string? Description);

public enum FinancialOperationFailure
{
    None,
    AccountNotFound,
    AccountInactive,
    InsufficientFunds,
    BalanceLimitExceeded,
    CurrencyMismatch,
    IdempotencyConflict
}

public sealed record FinancialOperationExecution(
    FinancialTransaction? Transaction,
    FinancialOperationFailure Failure,
    bool IsDuplicate)
{
    public bool IsSuccessful => Failure == FinancialOperationFailure.None;
}

public interface IFinancialOperationService
{
    Task<FinancialOperationExecution> ExecuteAsync(
        FinancialOperationRequest request,
        CancellationToken cancellationToken = default);
}
