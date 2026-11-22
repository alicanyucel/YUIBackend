using FluentValidation;
using YUIBackend.Application.Features.FinancialTransactions.GetFinancialTransaction;

namespace YUIBackend.Application.Features.FinancialTransactions.GetAccountTransaction;

public sealed class GetAccountTransactionHistoryQueryValidator : AbstractValidator<GetAccountTransactionHistoryQuery>
{
    public GetAccountTransactionHistoryQueryValidator()
    {
        RuleFor(query => query.AccountId).NotEmpty();
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.FromUtc).LessThanOrEqualTo(query => query.ToUtc)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue);
        RuleFor(query => query.MinimumAmount).GreaterThanOrEqualTo(0)
            .When(query => query.MinimumAmount.HasValue);
        RuleFor(query => query.MaximumAmount).GreaterThanOrEqualTo(query => query.MinimumAmount)
            .When(query => query.MaximumAmount.HasValue && query.MinimumAmount.HasValue);
        RuleFor(query => query.Type).Must(GetFinancialTransactionsQueryValidator.IsTransactionType)
            .When(query => query.Type is not null);
    }
}
