using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.FinancialTransactions;

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
            request.Descending,
            request.CustomerId);
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
