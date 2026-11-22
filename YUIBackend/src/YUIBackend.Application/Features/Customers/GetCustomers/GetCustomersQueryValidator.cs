using FluentValidation;

namespace YUIBackend.Application.Features.Customers.GetCustomers;

public sealed class GetCustomersQueryValidator : AbstractValidator<GetCustomersQuery>
{
    public GetCustomersQueryValidator()
    {
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
