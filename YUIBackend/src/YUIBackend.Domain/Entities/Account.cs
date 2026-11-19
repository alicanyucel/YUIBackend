using MyProject.Domain.Abstractions;

namespace MyProject.Domain.Entities;

public sealed class Account : Entity
{
    private Account() { }

    public Account(Guid customerId, string accountNumber, string currencyCode)
    {
        CustomerId = customerId;
        AccountNumber = accountNumber;
        CurrencyCode = currencyCode;
    }

    public Guid CustomerId { get; set; }
    public string AccountNumber { get; set; } = null!;
    public decimal Balance { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public AccountStatus Status { get; set; } = AccountStatus.Active;

    public Customer Customer { get; set; } = null!;
    public ICollection<FinancialTransaction> SourceTransactions { get; } = new List<FinancialTransaction>();
    public ICollection<FinancialTransaction> DestinationTransactions { get; } = new List<FinancialTransaction>();
}
