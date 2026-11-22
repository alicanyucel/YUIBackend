using MyProject.Application.Abstractions.FinancialOperations;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using YUIBackend.Application.Features.FinancialTransactions.DepositMoney;
using YUIBackend.Application.Features.FinancialTransactions.FinancialOperations;
using YUIBackend.Domain.Enums;

namespace YUIBackend.Application.Features.FinancialTransactions.WithdrawMoney;

internal sealed class WithdrawMoneyCommandHandler(IFinancialOperationService service)
    : ICommandHandler<WithdrawMoneyCommand, FinancialOperationResponse>
{
    public async Task<Result<FinancialOperationResponse>> Handle(
        WithdrawMoneyCommand request,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new FinancialOperationRequest(
            FinancialTransactionType.Withdrawal,
            request.Amount,
            request.AccountId,
            null,
            request.IdempotencyKey,
            request.Description), cancellationToken);
        return Result.Success(DepositMoneyCommandHandler.ToResponse(result));
    }
}
