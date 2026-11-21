using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;

namespace MyProject.Application.Features.FinancialTransactions;

internal sealed class GetFinancialTransactionByIdQueryHandler(IFinancialTransactionRepository repository)
    : IQueryHandler<GetFinancialTransactionByIdQuery, FinancialTransactionDto>
{
    public async Task<Result<FinancialTransactionDto>> Handle(
        GetFinancialTransactionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var transaction = await repository.GetByIdAsync(request.Id, cancellationToken);
        return transaction is null
            ? Result.Failure<FinancialTransactionDto>(Error.NotFound(nameof(FinancialTransaction)))
            : Result.Success(GetFinancialTransactionsQueryHandler.ToDto(transaction));
    }
}
