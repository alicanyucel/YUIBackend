using FluentValidation;
using YUIBackend.Application.Features.FinancialTransactions.DepositMoney;

namespace YUIBackend.Application.Features.FinancialTransactions.TransferMoney;

internal sealed class TransferMoneyCommandValidator : AbstractValidator<TransferMoneyCommand>
{
    public TransferMoneyCommandValidator()
    {
        RuleFor(command => command.SourceAccountId).NotEmpty();
        RuleFor(command => command.DestinationAccountId).NotEmpty()
            .NotEqual(command => command.SourceAccountId)
            .WithMessage("Source and destination accounts must be different.");
        DepositMoneyCommandValidator.ConfigureAmount(RuleFor(command => command.Amount));
        DepositMoneyCommandValidator.ConfigureIdempotencyKey(RuleFor(command => command.IdempotencyKey));
        RuleFor(command => command.Description).MaximumLength(500);
    }
}
