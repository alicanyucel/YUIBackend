using MyProject.Application.Abstractions.Cache;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Customers;

internal sealed class CreateCustomerCommandHandler(
    ICustomerRepository repository,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<CreateCustomerCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateCustomerCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (await repository.GetByEmailAsync(email, cancellationToken) is not null)
            return Result.Failure<Guid>(Error.Conflict(nameof(Customer)));

        var customer = new Customer(request.FirstName.Trim(), request.LastName.Trim(),
            email, request.PhoneNumber?.Trim());
        await repository.AddAsync(customer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("customers", cancellationToken);
        return Result.Success(customer.Id);
    }
}
