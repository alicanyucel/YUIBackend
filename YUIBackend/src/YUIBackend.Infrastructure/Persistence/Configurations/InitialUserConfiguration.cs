using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyProject.Domain.Entities;

namespace MyProject.Infrastructure.Persistence.Configurations;

internal sealed class InitialUserConfiguration : IEntityTypeConfiguration<InitialUser>
{
    public void Configure(EntityTypeBuilder<InitialUser> builder)
    {
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(user => user.NormalizedUsername)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(user => user.NormalizedUsername)
            .IsUnique();

        builder.Property(user => user.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(user => user.IsActive)
            .HasDefaultValue(true);
    }
}
