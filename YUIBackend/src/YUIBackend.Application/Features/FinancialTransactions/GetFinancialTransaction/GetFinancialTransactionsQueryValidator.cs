using FluentValidation;
using YUIBackend.Domain.Enums;

namespace YUIBackend.Application.Features.FinancialTransactions.GetFinancialTransaction;

public sealed class GetFinancialTransactionsQueryValidator : AbstractValidator<GetFinancialTransactionsQuery>
{
    public GetFinancialTransactionsQueryValidator()
    {
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.AccountId).NotEqual(Guid.Empty).When(query => query.AccountId.HasValue);
        RuleFor(query => query.FromUtc).LessThanOrEqualTo(query => query.ToUtc)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue);
        RuleFor(query => query.MinimumAmount).GreaterThanOrEqualTo(0)
            .When(query => query.MinimumAmount.HasValue);
        RuleFor(query => query.MaximumAmount).GreaterThanOrEqualTo(0)
            .When(query => query.MaximumAmount.HasValue);
        RuleFor(query => query.MaximumAmount).GreaterThanOrEqualTo(query => query.MinimumAmount)
            .When(query => query.MaximumAmount.HasValue && query.MinimumAmount.HasValue);
        RuleFor(query => query.Type).Must(IsTransactionType).When(query => query.Type is not null)
            .WithMessage("Type must be Deposit, Withdrawal, or Transfer.");
    }

    internal static bool IsTransactionType(string? type)
        => type is not null && FinancialTransactionType.List.Any(
            value => value.Name.Equals(type, StringComparison.OrdinalIgnoreCase));
}
