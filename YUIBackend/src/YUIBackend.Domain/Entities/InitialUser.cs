using MyProject.Domain.Abstractions;

namespace MyProject.Domain.Entities;

public sealed class InitialUser : Entity
{
    private InitialUser() { }

    public InitialUser(string username, string normalizedUsername, string passwordHash)
    {
        Username = username;
        NormalizedUsername = normalizedUsername;
        PasswordHash = passwordHash;
    }

    public string Username { get; private set; } = null!;
    public string NormalizedUsername { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    public void UpdatePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        MarkUpdated();
    }
}
