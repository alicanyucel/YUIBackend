using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using YUIBackend.Application.DTOS;

namespace YUIBackend.Application.Features.FinancialTransactions.GetFinancialTransaction;

public sealed record GetFinancialTransactionsQuery(
    Guid? AccountId = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Type = null,
    decimal? MinimumAmount = null,
    decimal? MaximumAmount = null,
    int PageNumber = 1,
    int PageSize = 20,
    bool Descending = true,
    Guid? CustomerId = null) : IQuery<PagedResult<FinancialTransactionDto>>;
