using MyProject.Application.Abstractions.Cache;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Customers;

internal sealed class DeleteCustomerCommandHandler(
    ICustomerRepository repository,
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork,
    ICacheService cache) : ICommandHandler<DeleteCustomerCommand>
{
    public async Task<Result> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (customer is null)
            return Result.Failure(Error.NotFound(nameof(Customer)));

        if ((await accountRepository.GetByCustomerIdAsync(customer.Id, cancellationToken)).Any())
            return Result.Failure(Error.Validation(nameof(Customer), "Customer with accounts cannot be deleted."));

        customer.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("customers", cancellationToken);
        return Result.Success();
    }
}
