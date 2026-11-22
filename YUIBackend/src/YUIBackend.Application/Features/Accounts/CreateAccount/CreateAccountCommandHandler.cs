using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;
using YUIBackend.Application.DTOS;
using YUIBackend.Application.Features.Accounts.GetAccounts;

namespace YUIBackend.Application.Features.Accounts.CreateAccount;

internal sealed class CreateAccountCommandHandler(
    IAccountRepository accountRepository,
    ICustomerRepository customerRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateAccountCommand, AccountDto>
{
    public async Task<Result<AccountDto>> Handle(
        CreateAccountCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
            return Result.Failure<AccountDto>(Error.NotFound(nameof(Customer)));
        if (!customer.IsActive)
            return Result.Failure<AccountDto>(Error.Validation(nameof(Customer), "An inactive customer cannot open an account."));

        if (await accountRepository.GetByAccountNumberAsync(request.AccountNumber, cancellationToken) is not null)
            return Result.Failure<AccountDto>(Error.Conflict(nameof(Account)));

        var account = new Account(request.CustomerId, request.AccountNumber.Trim(),
            request.CurrencyCode.ToUpperInvariant(), request.Balance);
        await accountRepository.AddAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(GetAccountsQueryHandler.ToDto(account));
    }
}
