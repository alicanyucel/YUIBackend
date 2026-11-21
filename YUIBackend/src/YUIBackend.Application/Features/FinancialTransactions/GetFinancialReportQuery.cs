using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;

namespace MyProject.Application.Features.FinancialTransactions;

public sealed record GetFinancialReportQuery(
    Guid? AccountId,
    DateTime FromUtc,
    DateTime ToUtc,
    string Period = "Daily",
    Guid? CustomerId = null) : IQuery<FinancialReport>;
