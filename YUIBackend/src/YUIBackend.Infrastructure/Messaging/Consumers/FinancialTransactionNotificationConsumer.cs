using MassTransit;
using Microsoft.Extensions.Logging;

namespace MyProject.Infrastructure.Messaging.Consumers;

public sealed class FinancialTransactionNotificationConsumer(ILogger<FinancialTransactionNotificationConsumer> logger)
    : IConsumer<FinancialTransactionNotification>
{
    public Task Consume(ConsumeContext<FinancialTransactionNotification> context)
    {
        var notification = context.Message;
        if (notification.Status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation(
                "Financial transaction succeeded. TransactionNumber: {TransactionNumber}, Type: {Type}, Amount: {Amount}, SourceAccountId: {SourceAccountId}, DestinationAccountId: {DestinationAccountId}",
                notification.TransactionNumber,
                notification.Type,
                notification.Amount,
                notification.SourceAccountId,
                notification.DestinationAccountId);
        }
        else
        {
            logger.LogWarning(
                "Financial transaction failed. TransactionNumber: {TransactionNumber}, Type: {Type}, Amount: {Amount}, FailureReason: {FailureReason}, SourceAccountId: {SourceAccountId}, DestinationAccountId: {DestinationAccountId}",
                notification.TransactionNumber,
                notification.Type,
                notification.Amount,
                notification.FailureReason,
                notification.SourceAccountId,
                notification.DestinationAccountId);
        }

        return Task.CompletedTask;
    }
}
