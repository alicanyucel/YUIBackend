using FluentValidation;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.FinancialTransactions;

public sealed record FinancialTransactionDto(
    Guid Id,
    string TransactionNumber,
    decimal Amount,
    string Type,
    string Status,
    string? Description,
    DateTime OccurredAt,
    Guid? SourceAccountId,
    Guid? DestinationAccountId);

public sealed record GetFinancialTransactionsQuery(int PageNumber = 1, int PageSize = 20)
    : IQuery<PagedResult<FinancialTransactionDto>>;

public sealed class GetFinancialTransactionsQueryValidator : AbstractValidator<GetFinancialTransactionsQuery>
{
    public GetFinancialTransactionsQueryValidator()
    {
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class GetFinancialTransactionsQueryHandler(IFinancialTransactionRepository repository)
    : IQueryHandler<GetFinancialTransactionsQuery, PagedResult<FinancialTransactionDto>>
{
    public async Task<Result<PagedResult<FinancialTransactionDto>>> Handle(
        GetFinancialTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await repository.GetPageAsync(request.PageNumber, request.PageSize, cancellationToken);
        return Result.Success(new PagedResult<FinancialTransactionDto>(page.Items.Select(ToDto).ToList(),
            page.TotalCount, page.PageNumber, page.PageSize));
    }

    internal static FinancialTransactionDto ToDto(FinancialTransaction transaction) => new(
        transaction.Id, transaction.TransactionNumber, transaction.Amount,
        transaction.Type.Name, transaction.Status.Name, transaction.Description,
        transaction.OccurredAt, transaction.SourceAccountId, transaction.DestinationAccountId);
}

public sealed record GetFinancialTransactionByIdQuery(Guid Id) : IQuery<FinancialTransactionDto>;

internal sealed class GetFinancialTransactionByIdQueryHandler(IFinancialTransactionRepository repository)
    : IQueryHandler<GetFinancialTransactionByIdQuery, FinancialTransactionDto>
{
    public async Task<Result<FinancialTransactionDto>> Handle(
        GetFinancialTransactionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var transaction = await repository.GetByIdAsync(request.Id, cancellationToken);
        return transaction is null
            ? Result.Failure<FinancialTransactionDto>(Error.NotFound(nameof(FinancialTransaction)))
            : Result.Success(GetFinancialTransactionsQueryHandler.ToDto(transaction));
    }
}

public sealed record GetAccountTransactionHistoryQuery(Guid AccountId, int PageNumber = 1, int PageSize = 20)
    : IQuery<PagedResult<FinancialTransactionDto>>;

public sealed class GetAccountTransactionHistoryQueryValidator : AbstractValidator<GetAccountTransactionHistoryQuery>
{
    public GetAccountTransactionHistoryQueryValidator()
    {
        RuleFor(query => query.AccountId).NotEmpty();
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class GetAccountTransactionHistoryQueryHandler(
    IFinancialTransactionRepository repository,
    IAccountRepository accountRepository)
    : IQueryHandler<GetAccountTransactionHistoryQuery, PagedResult<FinancialTransactionDto>>
{
    public async Task<Result<PagedResult<FinancialTransactionDto>>> Handle(
        GetAccountTransactionHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (await accountRepository.GetByIdAsync(request.AccountId, cancellationToken) is null)
            return Result.Failure<PagedResult<FinancialTransactionDto>>(Error.NotFound(nameof(Account)));

        var page = await repository.GetPageByAccountIdAsync(
            request.AccountId, request.PageNumber, request.PageSize, cancellationToken);
        return Result.Success(new PagedResult<FinancialTransactionDto>(page.Items
            .Select(GetFinancialTransactionsQueryHandler.ToDto).ToList(),
            page.TotalCount, page.PageNumber, page.PageSize));
    }
}
