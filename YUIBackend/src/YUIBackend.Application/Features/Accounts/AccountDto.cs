namespace MyProject.Application.Features.Accounts;

public sealed record AccountDto(
    Guid Id,
    Guid CustomerId,
    string AccountNumber,
    decimal Balance,
    string CurrencyCode,
    string Status,
    DateTime CreatedAt);
