namespace YUIBackend.Application.Features.DTOS;

public sealed record CustomerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    DateTime CreatedAt);
