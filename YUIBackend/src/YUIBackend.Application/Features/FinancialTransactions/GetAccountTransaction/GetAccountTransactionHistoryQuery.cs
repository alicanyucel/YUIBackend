using MyProject.Application.Abstractions.Messaging;
using MyProject.Application.Features.FinancialTransactions;
using MyProject.Domain.Abstractions;

namespace YUIBackend.Application.Features.FinancialTransactions.GetAccountTransaction;

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
