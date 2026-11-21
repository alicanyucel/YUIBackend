using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;

namespace MyProject.Application.Features.Accounts;

public sealed record GetAccountsQuery(int PageNumber = 1, int PageSize = 20, Guid? CustomerId = null)
    : IQuery<PagedResult<AccountDto>>;
