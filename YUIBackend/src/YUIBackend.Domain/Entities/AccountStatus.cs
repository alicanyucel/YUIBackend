using ACY.SmartEnum;

namespace MyProject.Domain.Entities;

public sealed class AccountStatus : SmartEnums<AccountStatus>
{
    private AccountStatus(string name, int value) : base(name, value) { }

    public static readonly AccountStatus Active = new(nameof(Active), 1);
    public static readonly AccountStatus Inactive = new(nameof(Inactive), 2);
    public static readonly AccountStatus Closed = new(nameof(Closed), 3);

    static AccountStatus() { }
}
