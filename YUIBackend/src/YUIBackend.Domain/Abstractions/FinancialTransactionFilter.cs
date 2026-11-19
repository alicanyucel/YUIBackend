using MyProject.Domain.Entities;

namespace MyProject.Domain.Abstractions;

public sealed record FinancialTransactionFilter(
    Guid? AccountId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    FinancialTransactionType? Type,
    decimal? MinimumAmount,
    decimal? MaximumAmount,
    bool Descending = true);

public enum FinancialReportPeriodKind
{
    Daily,
    Weekly,
    Monthly
}

public sealed record FinancialReportFilter(
    Guid? AccountId,
    DateTime FromUtc,
    DateTime ToUtc);

public sealed record FinancialReportPeriod(
    DateTime PeriodStartUtc,
    decimal TotalMoneyIn,
    decimal TotalMoneyOut,
    int SucceededCount,
    int FailedCount);

public sealed record FinancialReport(
    decimal TotalMoneyIn,
    decimal TotalMoneyOut,
    int SucceededCount,
    int FailedCount,
    IReadOnlyList<FinancialReportPeriod> Periods);
