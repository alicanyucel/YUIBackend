using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;
using YUIBackend.Application.Features.FinancialTransactions.GetFinancialReport;

namespace YUIBackend.Application.Features.FinancialTransactions.FinancialOperations;

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
        var filter = new FinancialReportFilter(request.AccountId, request.FromUtc, request.ToUtc, request.CustomerId);
        return Result.Success(await transactionRepository.GetReportAsync(filter, period, cancellationToken));
    }
}
