using AutoMapper;
using MyProject.Application.Abstractions.Cache;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Application.Abstractions.MultiTenancy;
using MyProject.Application.Features.Products.Dtos;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Products.Queries.GetProductById;

internal sealed class GetProductByIdQueryHandler(
    IProductRepository productRepository,
    IMapper mapper,
    ICacheService cache,
    ITenantService tenantService) : IQueryHandler<GetProductByIdQuery, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantService.GetCurrentTenantId();
        var page = await cache.GetOrCreateAsync("products", new { request.Id, tenantId }, async () =>
        {
            var product = await productRepository.GetByIdAsync(request.Id, cancellationToken);
            var items = product is null
                ? Array.Empty<ProductDto>()
                : [mapper.Map<ProductDto>(product)];
            return new PagedResult<ProductDto>(items, items.Length, 1, 1);
        }, cancellationToken);

        return page.Items.Count == 0
            ? Result.Failure<ProductDto>(Error.NotFound("Product"))
            : Result.Success(page.Items[0]);
    }
}
