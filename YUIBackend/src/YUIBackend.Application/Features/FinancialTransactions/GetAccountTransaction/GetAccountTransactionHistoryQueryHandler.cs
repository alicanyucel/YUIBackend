using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;
using YUIBackend.Application.DTOS;
using YUIBackend.Application.Features.FinancialTransactions.GetFinancialTransaction;
using YUIBackend.Domain.Enums;

namespace YUIBackend.Application.Features.FinancialTransactions.GetAccountTransaction;

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
