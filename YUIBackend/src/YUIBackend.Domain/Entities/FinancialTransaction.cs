using MyProject.Domain.Abstractions;

namespace MyProject.Domain.Entities;

public sealed class FinancialTransaction : Entity
{
    private FinancialTransaction() { }

    public FinancialTransaction(
        string transactionNumber,
        decimal amount,
        FinancialTransactionType type,
        Guid? sourceAccountId,
        Guid? destinationAccountId,
        string idempotencyKey)
    {
        TransactionNumber = transactionNumber;
        Amount = amount;
        Type = type;
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
        IdempotencyKey = idempotencyKey;
    }

    public string TransactionNumber { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public FinancialTransactionType Type { get; private set; } = null!;
    public FinancialTransactionStatus Status { get; private set; } = FinancialTransactionStatus.Pending;
    public string? Description { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime OccurredAt { get; private set; } = DateTime.UtcNow;
    public Guid? SourceAccountId { get; private set; }
    public Guid? DestinationAccountId { get; private set; }
    public string? IdempotencyKey { get; private set; }

    public Account? SourceAccount { get; private set; }
    public Account? DestinationAccount { get; private set; }

    public void SetDescription(string description)
        => Description = description;

    public void MarkSucceeded()
    {
        Status = FinancialTransactionStatus.Succeeded;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        Status = FinancialTransactionStatus.Failed;
        FailureReason = reason;
    }

    public override void SoftDelete()
        => throw new InvalidOperationException("Financial transaction history cannot be deleted.");
}
