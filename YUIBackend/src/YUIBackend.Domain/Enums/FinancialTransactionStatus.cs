using ACY.SmartEnum;
// Ali Can Yücel SmartEnum Kütüphanesi Özel Kütüphanem 

namespace YUIBackend.Domain.Enums;

public sealed class FinancialTransactionStatus : SmartEnums<FinancialTransactionStatus>
{
    private FinancialTransactionStatus(string name, int value) : base(name, value) { }

    public static readonly FinancialTransactionStatus Pending = new(nameof(Pending), 1);
    public static readonly FinancialTransactionStatus Succeeded = new(nameof(Succeeded), 2);
    public static readonly FinancialTransactionStatus Failed = new(nameof(Failed), 3);
    public static readonly FinancialTransactionStatus Cancelled = new(nameof(Cancelled), 4);

    static FinancialTransactionStatus() { }
}
