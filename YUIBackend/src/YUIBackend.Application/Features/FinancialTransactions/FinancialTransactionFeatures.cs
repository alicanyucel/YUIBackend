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
    string? FailureReason,
    DateTime OccurredAt,
    Guid? SourceAccountId,
    Guid? DestinationAccountId);

public sealed record GetFinancialTransactionsQuery(
    Guid? AccountId = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Type = null,
    decimal? MinimumAmount = null,
    decimal? MaximumAmount = null,
    int PageNumber = 1,
    int PageSize = 20,
    bool Descending = true) : IQuery<PagedResult<FinancialTransactionDto>>;

public sealed class GetFinancialTransactionsQueryValidator : AbstractValidator<GetFinancialTransactionsQuery>
{
    public GetFinancialTransactionsQueryValidator()
    {
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.AccountId).NotEqual(Guid.Empty).When(query => query.AccountId.HasValue);
        RuleFor(query => query.FromUtc).LessThanOrEqualTo(query => query.ToUtc)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue);
        RuleFor(query => query.MinimumAmount).GreaterThanOrEqualTo(0)
            .When(query => query.MinimumAmount.HasValue);
        RuleFor(query => query.MaximumAmount).GreaterThanOrEqualTo(0)
            .When(query => query.MaximumAmount.HasValue);
        RuleFor(query => query.MaximumAmount).GreaterThanOrEqualTo(query => query.MinimumAmount)
            .When(query => query.MaximumAmount.HasValue && query.MinimumAmount.HasValue);
        RuleFor(query => query.Type).Must(IsTransactionType).When(query => query.Type is not null)
            .WithMessage("Type must be Deposit, Withdrawal, or Transfer.");
    }

    internal static bool IsTransactionType(string? type)
        => type is not null && FinancialTransactionType.List.Any(
            value => value.Name.Equals(type, StringComparison.OrdinalIgnoreCase));
}

internal sealed class GetFinancialTransactionsQueryHandler(IFinancialTransactionRepository repository)
    : IQueryHandler<GetFinancialTransactionsQuery, PagedResult<FinancialTransactionDto>>
{
    public async Task<Result<PagedResult<FinancialTransactionDto>>> Handle(
        GetFinancialTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var filter = new FinancialTransactionFilter(
            request.AccountId,
            request.FromUtc,
            request.ToUtc,
            request.Type is null ? null : FinancialTransactionType.FromName(request.Type),
            request.MinimumAmount,
            request.MaximumAmount,
            request.Descending);
        var page = await repository.GetFilteredPageAsync(
            filter, request.PageNumber, request.PageSize, cancellationToken);
        return Result.Success(new PagedResult<FinancialTransactionDto>(page.Items.Select(ToDto).ToList(),
            page.TotalCount, page.PageNumber, page.PageSize));
    }

    internal static FinancialTransactionDto ToDto(FinancialTransaction transaction) => new(
        transaction.Id, transaction.TransactionNumber, transaction.Amount,
        transaction.Type.Name, transaction.Status.Name, transaction.Description,
        transaction.FailureReason, transaction.OccurredAt,
        transaction.SourceAccountId, transaction.DestinationAccountId);
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

public sealed record GetAccountTransactionHistoryQuery(
    Guid AccountId,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Type = null,
    decimal? MinimumAmount = null,
    decimal? MaximumAmount = null,
    int PageNumber = 1,
    int PageSize = 20,
    bool Descending = true) : IQuery<PagedResult<FinancialTransactionDto>>;

public sealed class GetAccountTransactionHistoryQueryValidator : AbstractValidator<GetAccountTransactionHistoryQuery>
{
    public GetAccountTransactionHistoryQueryValidator()
    {
        RuleFor(query => query.AccountId).NotEmpty();
        RuleFor(query => query.PageNumber).InclusiveBetween(1, 1_000_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.FromUtc).LessThanOrEqualTo(query => query.ToUtc)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue);
        RuleFor(query => query.MinimumAmount).GreaterThanOrEqualTo(0)
            .When(query => query.MinimumAmount.HasValue);
        RuleFor(query => query.MaximumAmount).GreaterThanOrEqualTo(query => query.MinimumAmount)
            .When(query => query.MaximumAmount.HasValue && query.MinimumAmount.HasValue);
        RuleFor(query => query.Type).Must(GetFinancialTransactionsQueryValidator.IsTransactionType)
            .When(query => query.Type is not null);
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

        var filter = new FinancialTransactionFilter(
            request.AccountId,
            request.FromUtc,
            request.ToUtc,
            request.Type is null ? null : FinancialTransactionType.FromName(request.Type),
            request.MinimumAmount,
            request.MaximumAmount,
            request.Descending);
        var page = await repository.GetFilteredPageAsync(
            filter, request.PageNumber, request.PageSize, cancellationToken);
        return Result.Success(new PagedResult<FinancialTransactionDto>(page.Items
            .Select(GetFinancialTransactionsQueryHandler.ToDto).ToList(),
            page.TotalCount, page.PageNumber, page.PageSize));
    }
}

public sealed record GetFinancialReportQuery(
    Guid? AccountId,
    DateTime FromUtc,
    DateTime ToUtc,
    string Period = "Daily") : IQuery<FinancialReport>;

public sealed class GetFinancialReportQueryValidator : AbstractValidator<GetFinancialReportQuery>
{
    public GetFinancialReportQueryValidator()
    {
        RuleFor(query => query.AccountId).NotEqual(Guid.Empty).When(query => query.AccountId.HasValue);
        RuleFor(query => query.FromUtc).LessThan(query => query.ToUtc);
        RuleFor(query => query.ToUtc).LessThanOrEqualTo(query => query.FromUtc.AddYears(5));
        RuleFor(query => query.Period).Must(period =>
                Enum.TryParse<FinancialReportPeriodKind>(period, true, out _))
            .WithMessage("Period must be Daily, Weekly, or Monthly.");
    }
}

internal sealed class GetFinancialReportQueryHandler(
    IFinancialTransactionRepository transactionRepository,
    IAccountRepository accountRepository)
    : IQueryHandler<GetFinancialReportQuery, FinancialReport>
{
    public async Task<Result<FinancialReport>> Handle(
        GetFinancialReportQuery request,
        CancellationToken cancellationToken)
    {
        if (request.AccountId is Guid accountId &&
            await accountRepository.GetByIdAsync(accountId, cancellationToken) is null)
            return Result.Failure<FinancialReport>(Error.NotFound(nameof(Account)));

        var period = Enum.Parse<FinancialReportPeriodKind>(request.Period, true);
        var filter = new FinancialReportFilter(request.AccountId, request.FromUtc, request.ToUtc);
        return Result.Success(await transactionRepository.GetReportAsync(filter, period, cancellationToken));
    }
}
