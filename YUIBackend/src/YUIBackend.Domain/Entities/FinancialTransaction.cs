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
        Guid? destinationAccountId)
    {
        TransactionNumber = transactionNumber;
        Amount = amount;
        Type = type;
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
    }

    public string TransactionNumber { get; set; } = null!;
    public decimal Amount { get; set; }
    public FinancialTransactionType Type { get; set; }
    public FinancialTransactionStatus Status { get; set; } = FinancialTransactionStatus.Pending;
    public string? Description { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public Guid? SourceAccountId { get; set; }
    public Guid? DestinationAccountId { get; set; }

    public Account? SourceAccount { get; set; }
    public Account? DestinationAccount { get; set; }
}
