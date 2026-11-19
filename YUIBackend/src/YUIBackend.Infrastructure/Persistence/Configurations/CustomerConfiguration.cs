using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyProject.Domain.Entities;

namespace MyProject.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(customer => customer.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(customer => customer.Email)
            .IsRequired()
            .HasMaxLength(254);

        builder.HasIndex(customer => customer.Email)
            .IsUnique();

        builder.Property(customer => customer.PhoneNumber)
            .HasMaxLength(30);

        builder.HasMany(customer => customer.Accounts)
            .WithOne(account => account.Customer)
            .HasForeignKey(account => account.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
