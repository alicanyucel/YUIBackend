using MyProject.Domain.Abstractions;

namespace MyProject.Domain.Entities;

public sealed class InitialUser : Entity
{
    private InitialUser() { }

    public InitialUser(
        string username,
        string normalizedUsername,
        string passwordHash,
        Guid? customerId = null,
        bool isAdmin = false)
    {
        Username = username;
        NormalizedUsername = normalizedUsername;
        PasswordHash = passwordHash;
        CustomerId = customerId;
        IsAdmin = isAdmin;
    }

    public string Username { get; private set; } = null!;
    public string NormalizedUsername { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public Guid? CustomerId { get; private set; }
    public bool IsAdmin { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void UpdatePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        MarkUpdated();
    }
}
