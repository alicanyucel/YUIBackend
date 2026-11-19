using ACY.SmartEnum;

namespace MyProject.Domain.Entities;

public sealed class FinancialTransactionType : SmartEnums<FinancialTransactionType>
{
    private FinancialTransactionType(string name, int value) : base(name, value) { }

    public static readonly FinancialTransactionType Deposit = new(nameof(Deposit), 1);
    public static readonly FinancialTransactionType Withdrawal = new(nameof(Withdrawal), 2);
    public static readonly FinancialTransactionType Transfer = new(nameof(Transfer), 3);
}
