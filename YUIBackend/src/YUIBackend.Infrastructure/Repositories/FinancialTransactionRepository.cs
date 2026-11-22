using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;
using MyProject.Infrastructure.Persistence;
using YUIBackend.Domain.Enums;

namespace MyProject.Infrastructure.Repositories;

internal sealed class FinancialTransactionRepository(AppDbContext context)
    : Repository<FinancialTransaction>(context), IFinancialTransactionRepository
{
    public override void Update(FinancialTransaction entity)
        => throw new InvalidOperationException("Financial transaction history is immutable.");

    public override void Delete(FinancialTransaction entity)
        => throw new InvalidOperationException("Financial transaction history cannot be deleted.");

    public Task<FinancialTransaction?> GetByTransactionNumberAsync(
        string transactionNumber,
        CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(
            transaction => transaction.TransactionNumber == transactionNumber,
            cancellationToken);

    public async Task<IEnumerable<FinancialTransaction>> GetByAccountIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
        => await DbSet.Where(transaction =>
                transaction.SourceAccountId == accountId || transaction.DestinationAccountId == accountId)
            .OrderByDescending(transaction => transaction.OccurredAt)
            .ToListAsync(cancellationToken);

    public override async Task<PagedResult<FinancialTransaction>> GetPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();
        return await query.ToPagedResultAsync(
            source => source.OrderByDescending(transaction => transaction.OccurredAt)
                .ThenByDescending(transaction => transaction.Id),
            source => source.OrderBy(transaction => transaction.OccurredAt)
                .ThenBy(transaction => transaction.Id),
            pageNumber,
            pageSize,
            cancellationToken);
    }

    public async Task<PagedResult<FinancialTransaction>> GetFilteredPageAsync(
        FinancialTransactionFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(DbSet.AsNoTracking(), filter);
        return filter.Descending
            ? await query.ToPagedResultAsync(
                source => source.OrderByDescending(transaction => transaction.OccurredAt)
                    .ThenByDescending(transaction => transaction.Id),
                source => source.OrderBy(transaction => transaction.OccurredAt)
                    .ThenBy(transaction => transaction.Id),
                pageNumber,
                pageSize,
                cancellationToken)
            : await query.ToPagedResultAsync(
                source => source.OrderBy(transaction => transaction.OccurredAt)
                    .ThenBy(transaction => transaction.Id),
                source => source.OrderByDescending(transaction => transaction.OccurredAt)
                    .ThenByDescending(transaction => transaction.Id),
                pageNumber,
                pageSize,
                cancellationToken);
    }

    public Task<FinancialTransaction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => DbSet.AsNoTracking()
            .SingleOrDefaultAsync(transaction => transaction.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<FinancialReport> GetReportAsync(
        FinancialReportFilter filter,
        FinancialReportPeriodKind periodKind,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .Where(transaction => transaction.OccurredAt >= filter.FromUtc && transaction.OccurredAt < filter.ToUtc);
        if (filter.AccountId is Guid accountId)
        {
            query = query.Where(transaction =>
                transaction.SourceAccountId == accountId || transaction.DestinationAccountId == accountId);
        }
        if (filter.CustomerId is Guid customerId)
        {
            query = query.Where(transaction =>
                transaction.SourceAccount != null && transaction.SourceAccount.CustomerId == customerId ||
                transaction.DestinationAccount != null && transaction.DestinationAccount.CustomerId == customerId);
        }

        var epoch = DateTime.UnixEpoch;
        List<RawReportPeriod> rawPeriods;
        if (periodKind == FinancialReportPeriodKind.Daily)
        {
            rawPeriods = await query.GroupBy(transaction =>
                    EF.Functions.DateDiffDay(epoch, transaction.OccurredAt))
                .Select(CreateReportProjection(filter.AccountId, filter.CustomerId))
                .ToListAsync(cancellationToken);
        }
        else if (periodKind == FinancialReportPeriodKind.Weekly)
        {
            var mondayEpoch = new DateTime(1970, 1, 5, 0, 0, 0, DateTimeKind.Utc);
            rawPeriods = await query.GroupBy(transaction =>
                    EF.Functions.DateDiffDay(mondayEpoch, transaction.OccurredAt) / 7)
                .Select(CreateReportProjection(filter.AccountId, filter.CustomerId))
                .ToListAsync(cancellationToken);
            epoch = mondayEpoch;
        }
        else
        {
            rawPeriods = await query.GroupBy(transaction =>
                    EF.Functions.DateDiffMonth(epoch, transaction.OccurredAt))
                .Select(CreateReportProjection(filter.AccountId, filter.CustomerId))
                .ToListAsync(cancellationToken);
        }

        var periods = rawPeriods.Select(period => new FinancialReportPeriod(
                periodKind == FinancialReportPeriodKind.Monthly
                    ? epoch.AddMonths(period.Bucket)
                    : epoch.AddDays(period.Bucket * (periodKind == FinancialReportPeriodKind.Weekly ? 7 : 1)),
                period.TotalMoneyIn,
                period.TotalMoneyOut,
                period.SucceededCount,
                period.FailedCount))
            .OrderBy(period => period.PeriodStartUtc)
            .ToList();

        return new FinancialReport(
            periods.Sum(period => period.TotalMoneyIn),
            periods.Sum(period => period.TotalMoneyOut),
            periods.Sum(period => period.SucceededCount),
            periods.Sum(period => period.FailedCount),
            periods);
    }

    private static IQueryable<FinancialTransaction> ApplyFilter(
        IQueryable<FinancialTransaction> query,
        FinancialTransactionFilter filter)
    {
        if (filter.AccountId is Guid accountId)
            query = query.Where(transaction => transaction.SourceAccountId == accountId ||
                transaction.DestinationAccountId == accountId);
        if (filter.CustomerId is Guid customerId)
            query = query.Where(transaction =>
                transaction.SourceAccount != null && transaction.SourceAccount.CustomerId == customerId ||
                transaction.DestinationAccount != null && transaction.DestinationAccount.CustomerId == customerId);
        if (filter.FromUtc is DateTime fromUtc)
            query = query.Where(transaction => transaction.OccurredAt >= fromUtc);
        if (filter.ToUtc is DateTime toUtc)
            query = query.Where(transaction => transaction.OccurredAt <= toUtc);
        if (filter.Type is not null)
            query = query.Where(transaction => transaction.Type == filter.Type);
        if (filter.MinimumAmount is decimal minimumAmount)
            query = query.Where(transaction => transaction.Amount >= minimumAmount);
        if (filter.MaximumAmount is decimal maximumAmount)
            query = query.Where(transaction => transaction.Amount <= maximumAmount);
        return query;
    }

    private static Expression<Func<IGrouping<int, FinancialTransaction>, RawReportPeriod>>
        CreateReportProjection(Guid? accountId, Guid? customerId)
        => group => new RawReportPeriod(
            group.Key,
            group.Sum(transaction => transaction.Status == FinancialTransactionStatus.Succeeded &&
                    (accountId != null ? transaction.DestinationAccountId == accountId :
                        customerId != null ? transaction.DestinationAccount != null &&
                            transaction.DestinationAccount.CustomerId == customerId :
                            transaction.DestinationAccountId != null)
                ? (decimal?)transaction.Amount : null) ?? 0m,
            group.Sum(transaction => transaction.Status == FinancialTransactionStatus.Succeeded &&
                    (accountId != null ? transaction.SourceAccountId == accountId :
                        customerId != null ? transaction.SourceAccount != null &&
                            transaction.SourceAccount.CustomerId == customerId :
                            transaction.SourceAccountId != null)
                ? (decimal?)transaction.Amount : null) ?? 0m,
            group.Count(transaction => transaction.Status == FinancialTransactionStatus.Succeeded),
            group.Count(transaction => transaction.Status == FinancialTransactionStatus.Failed));

    private sealed record RawReportPeriod(
        int Bucket,
        decimal TotalMoneyIn,
        decimal TotalMoneyOut,
        int SucceededCount,
        int FailedCount);
}
