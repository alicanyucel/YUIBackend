using FluentValidation;

namespace YUIBackend.Application.Features.Accounts.CreateAccount;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.AccountNumber).NotEmpty().MaximumLength(34);
        RuleFor(command => command.CurrencyCode).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$");
        RuleFor(command => command.Balance)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(9_999_999_999_999_999.99m)
            .Must(balance => decimal.Round(balance, 2) == balance)
            .WithMessage("Balance must be non-negative, within the supported range, and have at most two decimal places.");
    }
}
