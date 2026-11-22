using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace YUIBackend.Application.Features.Accounts.DeleteAccount;

internal sealed class DeleteAccountCommandHandler(
    IAccountRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteAccountCommand>
{
    public async Task<Result> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (account is null)
            return Result.Failure(Error.NotFound(nameof(Account)));
        if (account.Balance != 0)
            return Result.Failure(Error.Validation(nameof(Account), "An account with a non-zero balance cannot be deleted."));

        account.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
