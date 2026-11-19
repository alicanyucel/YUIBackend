using MyProject.Application.Abstractions.Cache;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Products.Commands.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id) : ICommand;

internal sealed class DeleteProductCommandHandler(
    IProductRepository repository,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<DeleteProductCommand>
{
    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
            return Result.Failure(Error.NotFound(nameof(Product)));

        repository.Delete(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("products", cancellationToken);
        return Result.Success();
    }
}
