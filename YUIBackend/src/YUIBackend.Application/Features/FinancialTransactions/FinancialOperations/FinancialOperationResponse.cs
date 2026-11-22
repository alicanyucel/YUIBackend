namespace YUIBackend.Application.Features.FinancialTransactions.FinancialOperations;

public sealed record FinancialOperationResponse(
    Guid? TransactionId,
    string? TransactionNumber,
    string Status,
    bool Succeeded,
    bool IsDuplicate,
    string? FailureReason);
