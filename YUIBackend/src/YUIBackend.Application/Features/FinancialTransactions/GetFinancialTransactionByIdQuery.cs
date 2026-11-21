using MyProject.Application.Abstractions.Messaging;

namespace MyProject.Application.Features.FinancialTransactions;

public sealed record GetFinancialTransactionByIdQuery(Guid Id) : IQuery<FinancialTransactionDto>;
