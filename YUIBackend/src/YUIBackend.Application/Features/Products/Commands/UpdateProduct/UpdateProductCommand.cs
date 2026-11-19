using FluentValidation;
using MyProject.Application.Abstractions.Cache;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Products.Commands.UpdateProduct;

public sealed record UpdateProductCommand(Guid Id, string Name, string Description, decimal Price, int Stock) : ICommand;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Price).GreaterThan(0);
        RuleFor(command => command.Stock).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateProductCommandHandler(
    IProductRepository repository,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<UpdateProductCommand>
{
    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
            return Result.Failure(Error.NotFound(nameof(Product)));

        var result = product.Update(request.Name, request.Description, request.Price, request.Stock);
        if (result.IsFailure)
            return result;

        repository.Update(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("products", cancellationToken);
        return Result.Success();
    }
}
