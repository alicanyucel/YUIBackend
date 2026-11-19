using FluentValidation;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.Accounts;

public sealed record AccountDto(
    Guid Id,
    Guid CustomerId,
    string AccountNumber,
    decimal Balance,
    string CurrencyCode,
    string Status,
    DateTime CreatedAt);

public sealed record GetAccountsQuery(int PageNumber = 1, int PageSize = 20)
    : IQuery<PagedResult<AccountDto>>;

public sealed class GetAccountsQueryValidator : AbstractValidator<GetAccountsQuery>
{
    public GetAccountsQueryValidator()
    {
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class GetAccountsQueryHandler(IAccountRepository repository)
    : IQueryHandler<GetAccountsQuery, PagedResult<AccountDto>>
{
    public async Task<Result<PagedResult<AccountDto>>> Handle(
        GetAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await repository.GetPageAsync(request.PageNumber, request.PageSize, cancellationToken);
        return Result.Success(new PagedResult<AccountDto>(page.Items.Select(ToDto).ToList(),
            page.TotalCount, page.PageNumber, page.PageSize));
    }

    internal static AccountDto ToDto(Account account) => new(
        account.Id, account.CustomerId, account.AccountNumber, account.Balance,
        account.CurrencyCode, account.Status.Name, account.CreatedAt);
}

public sealed record GetAccountByIdQuery(Guid Id) : IQuery<AccountDto>;

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

public sealed record CreateAccountCommand(Guid CustomerId, string AccountNumber, string CurrencyCode)
    : ICommand<Guid>;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.AccountNumber).NotEmpty().MaximumLength(34);
        RuleFor(command => command.CurrencyCode).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$");
    }
}

internal sealed class CreateAccountCommandHandler(
    IAccountRepository accountRepository,
    ICustomerRepository customerRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateAccountCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateAccountCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
            return Result.Failure<Guid>(Error.NotFound(nameof(Customer)));
        if (!customer.IsActive)
            return Result.Failure<Guid>(Error.Validation(nameof(Customer), "An inactive customer cannot open an account."));

        if (await accountRepository.GetByAccountNumberAsync(request.AccountNumber, cancellationToken) is not null)
            return Result.Failure<Guid>(Error.Conflict(nameof(Account)));

        var account = new Account(request.CustomerId, request.AccountNumber.Trim(),
            request.CurrencyCode.ToUpperInvariant());
        await accountRepository.AddAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(account.Id);
    }
}

public sealed record UpdateAccountCommand(Guid Id, string Status) : ICommand;

public sealed class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Status)
            .Must(status => !string.IsNullOrWhiteSpace(status) &&
                AccountStatus.List.Any(value => value.Name.Equals(status, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Status must be Active, Inactive, or Closed.");
    }
}

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

public sealed record DeleteAccountCommand(Guid Id) : ICommand;

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
