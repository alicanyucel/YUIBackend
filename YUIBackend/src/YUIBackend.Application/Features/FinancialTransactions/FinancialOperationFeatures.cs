using FluentValidation;
using MyProject.Application.Abstractions.FinancialOperations;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;

namespace MyProject.Application.Features.FinancialTransactions;

public sealed record DepositMoneyCommand(
    Guid AccountId,
    decimal Amount,
    string IdempotencyKey,
    string? Description) : ICommand<FinancialOperationResponse>;

public sealed record WithdrawMoneyCommand(
    Guid AccountId,
    decimal Amount,
    string IdempotencyKey,
    string? Description) : ICommand<FinancialOperationResponse>;

public sealed record TransferMoneyCommand(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string IdempotencyKey,
    string? Description) : ICommand<FinancialOperationResponse>;

public sealed record FinancialOperationResponse(
    Guid? TransactionId,
    string? TransactionNumber,
    string Status,
    bool Succeeded,
    bool IsDuplicate,
    string? FailureReason);

internal sealed class DepositMoneyCommandValidator : AbstractValidator<DepositMoneyCommand>
{
    public DepositMoneyCommandValidator()
    {
        RuleFor(command => command.AccountId).NotEmpty();
        ConfigureAmount(RuleFor(command => command.Amount));
        ConfigureIdempotencyKey(RuleFor(command => command.IdempotencyKey));
        RuleFor(command => command.Description).MaximumLength(500);
    }

    internal static void ConfigureAmount<T>(IRuleBuilderInitial<T, decimal> rule)
        => rule.GreaterThan(0).LessThanOrEqualTo(9_999_999_999_999_999.99m)
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage("Amount must be positive, within the supported balance range, and have at most two decimal places.");

    internal static void ConfigureIdempotencyKey<T>(IRuleBuilderInitial<T, string> rule)
        => rule.NotEmpty().MaximumLength(128)
            .Matches("^[A-Za-z0-9._:-]+$")
            .WithMessage("Idempotency-Key is required and may contain only letters, numbers, '.', '_', ':', or '-'.");
}

internal sealed class WithdrawMoneyCommandValidator : AbstractValidator<WithdrawMoneyCommand>
{
    public WithdrawMoneyCommandValidator()
    {
        RuleFor(command => command.AccountId).NotEmpty();
        DepositMoneyCommandValidator.ConfigureAmount(RuleFor(command => command.Amount));
        DepositMoneyCommandValidator.ConfigureIdempotencyKey(RuleFor(command => command.IdempotencyKey));
        RuleFor(command => command.Description).MaximumLength(500);
    }
}

internal sealed class TransferMoneyCommandValidator : AbstractValidator<TransferMoneyCommand>
{
    public TransferMoneyCommandValidator()
    {
        RuleFor(command => command.SourceAccountId).NotEmpty();
        RuleFor(command => command.DestinationAccountId).NotEmpty()
            .NotEqual(command => command.SourceAccountId)
            .WithMessage("Source and destination accounts must be different.");
        DepositMoneyCommandValidator.ConfigureAmount(RuleFor(command => command.Amount));
        DepositMoneyCommandValidator.ConfigureIdempotencyKey(RuleFor(command => command.IdempotencyKey));
        RuleFor(command => command.Description).MaximumLength(500);
    }
}

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
