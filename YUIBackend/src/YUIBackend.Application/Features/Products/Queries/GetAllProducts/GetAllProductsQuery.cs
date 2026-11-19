using FluentValidation;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Application.Features.Products.Dtos;

namespace MyProject.Application.Features.Products.Queries.GetAllProducts;

public sealed record GetAllProductsQuery(int PageNumber = 1, int PageSize = 20)
    : IQuery<PagedResult<ProductDto>>;

public sealed class GetAllProductsQueryValidator : AbstractValidator<GetAllProductsQuery>
{
    public GetAllProductsQueryValidator()
    {
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
