using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyProject.Domain.Entities;

namespace MyProject.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(account => account.Id);

        builder.Property(account => account.AccountNumber)
            .IsRequired()
            .HasMaxLength(34);

        builder.HasIndex(account => account.AccountNumber)
            .IsUnique();

        builder.Property(account => account.Balance)
            .HasPrecision(18, 2);

        builder.Property(account => account.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3)
            .IsUnicode(false);

        builder.Property(account => account.Status)
            .HasConversion(status => status.Name, name => AccountStatus.FromName(name))
            .HasMaxLength(16);

        builder.HasIndex(account => account.CustomerId);
    }
}
