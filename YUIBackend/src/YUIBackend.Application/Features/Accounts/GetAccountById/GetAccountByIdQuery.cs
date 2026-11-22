using MyProject.Application.Abstractions.Messaging;

namespace YUIBackend.Application.Features.Accounts.GetAccountById;

public sealed record GetAccountByIdQuery(Guid Id) : IQuery<AccountDto>;
