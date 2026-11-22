using MyProject.Application.Abstractions.Messaging;
using YUIBackend.Application.DTOS;

namespace YUIBackend.Application.Features.Accounts.GetAccountById;

public sealed record GetAccountByIdQuery(Guid Id) : IQuery<AccountDto>;
