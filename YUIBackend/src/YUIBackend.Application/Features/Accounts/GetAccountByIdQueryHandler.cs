using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Accounts;

internal sealed class GetAccountByIdQueryHandler(IAccountRepository repository)
    : IQueryHandler<GetAccountByIdQuery, AccountDto>
{
    public async Task<Result<AccountDto>> Handle(
        GetAccountByIdQuery request,
        CancellationToken cancellationToken)
    {
        var account = await repository.GetByIdAsync(request.Id, cancellationToken);
        return account is null
            ? Result.Failure<AccountDto>(Error.NotFound(nameof(Account)))
            : Result.Success(GetAccountsQueryHandler.ToDto(account));
    }
}
