using MyProject.Application.Abstractions.Cache;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Customers;

internal sealed class UpdateCustomerCommandHandler(
    ICustomerRepository repository,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<UpdateCustomerCommand>
{
    public async Task<Result> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (customer is null)
            return Result.Failure(Error.NotFound(nameof(Customer)));

        var email = request.Email.Trim();
        var existing = await repository.GetByEmailAsync(email, cancellationToken);
        if (existing is not null && existing.Id != customer.Id)
            return Result.Failure(Error.Conflict(nameof(Customer)));

        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.Email = email;
        customer.PhoneNumber = request.PhoneNumber?.Trim();
        if (request.IsActive is bool isActive)
            customer.IsActive = isActive;
        customer.MarkUpdated();
        repository.Update(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("customers", cancellationToken);
        return Result.Success();
    }
}
