using ACY.SmartEnum;
// Ali Can Yücel SmartEnum Kütüphanesi Özel Kütüphanem 
namespace YUIBackend.Domain.Enums;

public sealed class FinancialTransactionType : SmartEnums<FinancialTransactionType>
{
    private FinancialTransactionType(string name, int value) : base(name, value) { }

    public static readonly FinancialTransactionType Deposit = new(nameof(Deposit), 1);
    public static readonly FinancialTransactionType Withdrawal = new(nameof(Withdrawal), 2);
    public static readonly FinancialTransactionType Transfer = new(nameof(Transfer), 3);

    static FinancialTransactionType() { }
}
