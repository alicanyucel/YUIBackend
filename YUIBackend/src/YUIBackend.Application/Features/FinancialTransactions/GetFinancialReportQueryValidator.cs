using FluentValidation;
using MyProject.Domain.Abstractions;

namespace MyProject.Application.Features.FinancialTransactions;

public sealed class GetFinancialReportQueryValidator : AbstractValidator<GetFinancialReportQuery>
{
    public GetFinancialReportQueryValidator()
    {
        RuleFor(query => query.AccountId).NotEqual(Guid.Empty).When(query => query.AccountId.HasValue);
        RuleFor(query => query.FromUtc).LessThan(query => query.ToUtc);
        RuleFor(query => query.ToUtc).LessThanOrEqualTo(query => query.FromUtc.AddYears(5));
        RuleFor(query => query.Period).Must(period =>
                Enum.TryParse<FinancialReportPeriodKind>(period, true, out _))
            .WithMessage("Period must be Daily, Weekly, or Monthly.");
    }
}
