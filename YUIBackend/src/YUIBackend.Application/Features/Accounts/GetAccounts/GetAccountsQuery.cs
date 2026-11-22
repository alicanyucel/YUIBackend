using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using YUIBackend.Application.DTOS;

namespace YUIBackend.Application.Features.Accounts.GetAccounts;

public sealed record GetAccountsQuery(int PageNumber = 1, int PageSize = 20, Guid? CustomerId = null)
    : IQuery<PagedResult<AccountDto>>;
