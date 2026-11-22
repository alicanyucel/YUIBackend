using MyProject.Application.Abstractions.FinancialOperations;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using YUIBackend.Application.Features.FinancialTransactions.FinancialOperations;

namespace YUIBackend.Application.Features.FinancialTransactions.DepositMoney;

internal sealed class DepositMoneyCommandHandler(IFinancialOperationService service)
    : ICommandHandler<DepositMoneyCommand, FinancialOperationResponse>
{
    public Task<Result<FinancialOperationResponse>> Handle(
        DepositMoneyCommand request,
        CancellationToken cancellationToken)
        => ExecuteAsync(new FinancialOperationRequest(
            FinancialTransactionType.Deposit,
            request.Amount,
            null,
            request.AccountId,
            request.IdempotencyKey,
            request.Description), cancellationToken);

    private async Task<Result<FinancialOperationResponse>> ExecuteAsync(
        FinancialOperationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(request, cancellationToken);
        return Result.Success(ToResponse(result));
    }

    internal static FinancialOperationResponse ToResponse(FinancialOperationExecution execution)
        => new(execution.Transaction?.Id,
            execution.Transaction?.TransactionNumber,
            execution.Transaction?.Status.Name ?? FinancialTransactionStatus.Failed.Name,
            execution.IsSuccessful,
            execution.IsDuplicate,
            execution.IsSuccessful ? null : execution.Failure.ToString());
}
