using AutoMapper;
using MyProject.Application.Abstractions.Cache;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Application.Abstractions.MultiTenancy;
using MyProject.Application.Features.Products.Dtos;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Products.Queries.GetAllProducts;

internal sealed class GetAllProductsQueryHandler(
    IProductRepository productRepository,
    IMapper mapper,
    ICacheService cache,
    ITenantService tenantService) : IQueryHandler<GetAllProductsQuery, PagedResult<ProductDto>>
{
    public async Task<Result<PagedResult<ProductDto>>> Handle(
        GetAllProductsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantService.GetCurrentTenantId();
        var result = await cache.GetOrCreateAsync("products", new { request.PageNumber, request.PageSize, tenantId }, async () =>
        {
            var page = await productRepository.GetPageAsync(request.PageNumber, request.PageSize, cancellationToken);
            return new PagedResult<ProductDto>(mapper.Map<IReadOnlyList<ProductDto>>(page.Items),
                page.TotalCount, page.PageNumber, page.PageSize);
        }, cancellationToken);
        return Result.Success(result);
    }
}
