using MyProject.Application.Abstractions.Messaging;
using MyProject.Application.Features.FinancialTransactions;

namespace YUIBackend.Application.Features.FinancialTransactions.GetFinancialTransaction;

public sealed record GetFinancialTransactionByIdQuery(Guid Id) : IQuery<FinancialTransactionDto>;
