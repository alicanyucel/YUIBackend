using FluentAssertions;
using MyProject.Application.Features.Accounts;
using MyProject.Domain.Entities;

namespace MyProject.Tests.Application.Accounts;

public sealed class CreateAccountCommandTests
{
    [Fact]
    public void Validator_AcceptsOpeningBalanceWithinSupportedRange()
    {
        var command = new CreateAccountCommand(Guid.NewGuid(), "TR000000000000000000000000", "TRY", 2500.50m);

        var result = new CreateAccountCommandValidator().Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10000000000000000)]
    public void Validator_RejectsOutOfRangeOpeningBalance(decimal balance)
    {
        var command = new CreateAccountCommand(Guid.NewGuid(), "TR000000000000000000000000", "TRY", balance);

        var result = new CreateAccountCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(command.Balance));
    }

    [Fact]
    public void Validator_RejectsOpeningBalanceWithMoreThanTwoDecimalPlaces()
    {
        var command = new CreateAccountCommand(Guid.NewGuid(), "TR000000000000000000000000", "TRY", 10.001m);

        var result = new CreateAccountCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(command.Balance));
    }

    [Fact]
    public void Account_UsesProvidedOpeningBalance()
    {
        var account = new Account(Guid.NewGuid(), "TR000000000000000000000000", "TRY", 2500.50m);

        account.Balance.Should().Be(2500.50m);
    }

    [Fact]
    public void Account_DefaultsOpeningBalanceToZero()
    {
        var account = new Account(Guid.NewGuid(), "TR000000000000000000000000", "TRY");

        account.Balance.Should().Be(0);
    }
}
