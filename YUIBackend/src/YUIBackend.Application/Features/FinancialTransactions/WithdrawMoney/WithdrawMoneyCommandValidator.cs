using FluentValidation;
using YUIBackend.Application.Features.FinancialTransactions.DepositMoney;

namespace YUIBackend.Application.Features.FinancialTransactions.WithdrawMoney;

internal sealed class WithdrawMoneyCommandValidator : AbstractValidator<WithdrawMoneyCommand>
{
    public WithdrawMoneyCommandValidator()
    {
        RuleFor(command => command.AccountId).NotEmpty();
        DepositMoneyCommandValidator.ConfigureAmount(RuleFor(command => command.Amount));
        DepositMoneyCommandValidator.ConfigureIdempotencyKey(RuleFor(command => command.IdempotencyKey));
        RuleFor(command => command.Description).MaximumLength(500);
    }
}
