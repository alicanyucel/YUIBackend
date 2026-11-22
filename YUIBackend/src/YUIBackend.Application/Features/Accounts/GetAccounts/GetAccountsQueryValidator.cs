using FluentValidation;

namespace YUIBackend.Application.Features.Accounts.GetAccounts;

public sealed class GetAccountsQueryValidator : AbstractValidator<GetAccountsQuery>
{
    public GetAccountsQueryValidator()
    {
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
