namespace MyProject.Application.Features.FinancialTransactions;

public sealed record FinancialOperationResponse(
    Guid? TransactionId,
    string? TransactionNumber,
    string Status,
    bool Succeeded,
    bool IsDuplicate,
    string? FailureReason);
