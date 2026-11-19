namespace MyProject.Infrastructure.Messaging;

public sealed record FinancialTransactionNotification(
    Guid TransactionId,
    string TransactionNumber,
    string Type,
    string Status,
    decimal Amount,
    Guid? SourceAccountId,
    Guid? DestinationAccountId,
    string? FailureReason,
    DateTime OccurredAtUtc);
