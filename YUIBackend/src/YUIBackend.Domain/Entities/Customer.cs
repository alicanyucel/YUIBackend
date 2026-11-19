using MyProject.Domain.Abstractions;

namespace MyProject.Domain.Entities;

public sealed class Customer : Entity
{
    private Customer() { }

    public Customer(string firstName, string lastName, string email, string? phoneNumber = null)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
    }

    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Account> Accounts { get; } = new List<Account>();
}
