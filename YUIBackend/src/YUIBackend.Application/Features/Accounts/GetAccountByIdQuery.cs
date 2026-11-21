using MyProject.Application.Abstractions.Messaging;

namespace MyProject.Application.Features.Accounts;

public sealed record GetAccountByIdQuery(Guid Id) : IQuery<AccountDto>;
