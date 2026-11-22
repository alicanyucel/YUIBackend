namespace YUIBackend.Application.DTOS;

public sealed record FinancialTransactionDto(
    Guid Id,
    string TransactionNumber,
    decimal Amount,
    string Type,
    string Status,
    string? Description,
    string? FailureReason,
    DateTime OccurredAt,
    Guid? SourceAccountId,
    Guid? DestinationAccountId);
