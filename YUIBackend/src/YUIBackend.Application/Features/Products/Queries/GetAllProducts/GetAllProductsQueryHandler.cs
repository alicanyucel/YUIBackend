using AutoMapper;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Application.Features.Products.Dtos;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Products.Queries.GetAllProducts;

internal sealed class GetAllProductsQueryHandler(
    IProductRepository productRepository,
    IMapper mapper) : IQueryHandler<GetAllProductsQuery, PagedResult<ProductDto>>
{
    public async Task<Result<PagedResult<ProductDto>>> Handle(
        GetAllProductsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await productRepository.GetPageAsync(request.PageNumber, request.PageSize, cancellationToken);
        var result = new PagedResult<ProductDto>(mapper.Map<IReadOnlyList<ProductDto>>(page.Items),
            page.TotalCount, page.PageNumber, page.PageSize);
        return Result.Success(result);
    }
}
