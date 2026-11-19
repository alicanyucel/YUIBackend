using FluentAssertions;
using MyProject.Application.Abstractions.FinancialOperations;
using MyProject.Application.Features.FinancialTransactions;
using MyProject.Domain.Entities;
using NSubstitute;

namespace MyProject.Tests.Application.FinancialTransactions;

public sealed class FinancialOperationCommandTests
{
    [Fact]
    public void DepositValidator_RejectsAmountsWithMoreThanTwoDecimalPlaces()
    {
        var validator = new DepositMoneyCommandValidator();
        var command = new DepositMoneyCommand(Guid.NewGuid(), 10.001m, "deposit-1", null);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(command.Amount));
    }

    [Fact]
    public void DepositValidator_RequiresAnIdempotencyKey()
    {
        var validator = new DepositMoneyCommandValidator();
        var command = new DepositMoneyCommand(Guid.NewGuid(), 10m, string.Empty, null);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(command.IdempotencyKey));
    }

    [Fact]
    public void TransferValidator_RejectsTheSameSourceAndDestinationAccount()
    {
        var validator = new TransferMoneyCommandValidator();
        var accountId = Guid.NewGuid();
        var command = new TransferMoneyCommand(accountId, accountId, 10m, "transfer-1", null);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(command.DestinationAccountId));
    }

    [Fact]
    public async Task DepositHandler_PassesTheCorrectOperationToTheTransactionalService()
    {
        var service = Substitute.For<IFinancialOperationService>();
        var accountId = Guid.NewGuid();
        service.ExecuteAsync(Arg.Any<FinancialOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new FinancialOperationExecution(null, FinancialOperationFailure.AccountNotFound, false));
        var handler = new DepositMoneyCommandHandler(service);
        var command = new DepositMoneyCommand(accountId, 25.50m, "deposit-ref-1", "Cash deposit");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Succeeded.Should().BeFalse();
        result.Value.FailureReason.Should().Be(nameof(FinancialOperationFailure.AccountNotFound));
        await service.Received(1).ExecuteAsync(
            Arg.Is<FinancialOperationRequest>(request =>
                request.Type == FinancialTransactionType.Deposit &&
                request.Amount == 25.50m &&
                request.DestinationAccountId == accountId &&
                request.SourceAccountId == null &&
                request.IdempotencyKey == "deposit-ref-1" &&
                request.Description == "Cash deposit"),
            Arg.Any<CancellationToken>());
    }
}
