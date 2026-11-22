using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;

namespace YUIBackend.Application.Features.FinancialTransactions.GetFinancialReport;

public sealed record GetFinancialReportQuery(
    Guid? AccountId,
    DateTime FromUtc,
    DateTime ToUtc,
    string Period = "Daily",
    Guid? CustomerId = null) : IQuery<FinancialReport>;
