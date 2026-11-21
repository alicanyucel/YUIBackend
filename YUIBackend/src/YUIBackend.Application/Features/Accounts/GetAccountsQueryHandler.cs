using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Accounts;

internal sealed class GetAccountsQueryHandler(IAccountRepository repository)
    : IQueryHandler<GetAccountsQuery, PagedResult<AccountDto>>
{
    public async Task<Result<PagedResult<AccountDto>>> Handle(
        GetAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.CustomerId is Guid customerId
            ? await repository.GetPageByCustomerIdAsync(customerId, request.PageNumber, request.PageSize, cancellationToken)
            : await repository.GetPageAsync(request.PageNumber, request.PageSize, cancellationToken);
        return Result.Success(new PagedResult<AccountDto>(page.Items.Select(ToDto).ToList(),
            page.TotalCount, page.PageNumber, page.PageSize));
    }

    internal static AccountDto ToDto(Account account) => new(
        account.Id, account.CustomerId, account.AccountNumber, account.Balance,
        account.CurrencyCode, account.Status.Name, account.CreatedAt);
}
