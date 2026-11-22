using MyProject.Application.Abstractions.Messaging;
using YUIBackend.Application.DTOS;

namespace YUIBackend.Application.Features.FinancialTransactions.GetFinancialTransaction;

public sealed record GetFinancialTransactionByIdQuery(Guid Id) : IQuery<FinancialTransactionDto>;
