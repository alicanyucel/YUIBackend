using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Accounts;

internal sealed class UpdateAccountCommandHandler(
    IAccountRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateAccountCommand>
{
    public async Task<Result> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (account is null)
            return Result.Failure(Error.NotFound(nameof(Account)));

        var status = AccountStatus.FromName(request.Status);
        if (status == AccountStatus.Closed && account.Balance != 0)
            return Result.Failure(Error.Validation(nameof(Account), "An account with a non-zero balance cannot be closed."));

        account.Status = status;
        account.MarkUpdated();
        repository.Update(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
