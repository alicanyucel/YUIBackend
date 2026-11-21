using FluentValidation;

namespace MyProject.Application.Features.FinancialTransactions;

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
