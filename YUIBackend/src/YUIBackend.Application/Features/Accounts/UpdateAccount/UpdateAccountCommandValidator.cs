using FluentValidation;
using YUIBackend.Domain.Enums;

namespace YUIBackend.Application.Features.Accounts.UpdateAccount;

public sealed class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Status)
            .Must(status => !string.IsNullOrWhiteSpace(status) &&
                AccountStatus.List.Any(value => value.Name.Equals(status, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Status must be Active, Inactive, or Closed.");
    }
}
