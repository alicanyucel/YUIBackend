using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyProject.Domain.Entities;
using YUIBackend.Domain.Enums;

namespace MyProject.Infrastructure.Persistence.Configurations;

internal sealed class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.TransactionNumber)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(transaction => transaction.TransactionNumber)
            .IsUnique();

        builder.Property(transaction => transaction.Amount)
            .HasPrecision(18, 2);

        builder.Property(transaction => transaction.Type)
            .HasConversion(type => type.Name, name => FinancialTransactionType.FromName(name))
            .HasMaxLength(16);

        builder.Property(transaction => transaction.Status)
            .HasConversion(status => status.Name, name => FinancialTransactionStatus.FromName(name))
            .HasMaxLength(16);

        builder.Property(transaction => transaction.Description)
            .HasMaxLength(500);

        builder.Property(transaction => transaction.FailureReason)
            .HasMaxLength(300);

        builder.Property(transaction => transaction.IdempotencyKey)
            .HasMaxLength(128);

        builder.HasIndex(transaction => transaction.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        builder.HasIndex(transaction => new { transaction.OccurredAt, transaction.Status });
        builder.HasIndex(transaction => new { transaction.SourceAccountId, transaction.OccurredAt });
        builder.HasIndex(transaction => new { transaction.DestinationAccountId, transaction.OccurredAt });
        builder.HasIndex(transaction => new { transaction.Type, transaction.Status, transaction.OccurredAt });

        builder.HasOne(transaction => transaction.SourceAccount)
            .WithMany(account => account.SourceTransactions)
            .HasForeignKey(transaction => transaction.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(transaction => transaction.DestinationAccount)
            .WithMany(account => account.DestinationTransactions)
            .HasForeignKey(transaction => transaction.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
