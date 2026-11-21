using MyProject.Application.Abstractions.FinancialOperations;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;

namespace MyProject.Application.Features.FinancialTransactions;

internal sealed class TransferMoneyCommandHandler(IFinancialOperationService service)
    : ICommandHandler<TransferMoneyCommand, FinancialOperationResponse>
{
    public async Task<Result<FinancialOperationResponse>> Handle(
        TransferMoneyCommand request,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new FinancialOperationRequest(
            FinancialTransactionType.Transfer,
            request.Amount,
            request.SourceAccountId,
            request.DestinationAccountId,
            request.IdempotencyKey,
            request.Description), cancellationToken);
        return Result.Success(DepositMoneyCommandHandler.ToResponse(result));
    }
}
