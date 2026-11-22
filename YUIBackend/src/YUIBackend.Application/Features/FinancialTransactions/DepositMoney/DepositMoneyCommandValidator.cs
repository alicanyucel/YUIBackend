using FluentValidation;

namespace YUIBackend.Application.Features.FinancialTransactions.DepositMoney;

internal sealed class DepositMoneyCommandValidator : AbstractValidator<DepositMoneyCommand>
{
    public DepositMoneyCommandValidator()
    {
        RuleFor(command => command.AccountId).NotEmpty();
        ConfigureAmount(RuleFor(command => command.Amount));
        ConfigureIdempotencyKey(RuleFor(command => command.IdempotencyKey));
        RuleFor(command => command.Description).MaximumLength(500);
    }

    internal static void ConfigureAmount<T>(IRuleBuilderInitial<T, decimal> rule)
        => rule.GreaterThan(0).LessThanOrEqualTo(9_999_999_999_999_999.99m)
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage("Amount must be positive, within the supported balance range, and have at most two decimal places.");

    internal static void ConfigureIdempotencyKey<T>(IRuleBuilderInitial<T, string> rule)
        => rule.NotEmpty().MaximumLength(128)
            .Matches("^[A-Za-z0-9._:-]+$")
            .WithMessage("Idempotency-Key is required and may contain only letters, numbers, '.', '_', ':', or '-'.");
}
