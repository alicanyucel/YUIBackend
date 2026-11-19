using MassTransit;
using Microsoft.Extensions.Logging;
using MyProject.Domain.Entities;

namespace MyProject.Infrastructure.Messaging.Consumers;

public sealed class ProductCreatedConsumer(ILogger<ProductCreatedConsumer> logger)
    : IConsumer<ProductCreatedDomainEvent>
{
    public Task Consume(ConsumeContext<ProductCreatedDomainEvent> context)
    {
        logger.LogInformation("Product created event received: {ProductId} - {ProductName}",
            context.Message.ProductId, context.Message.ProductName);
        
        return Task.CompletedTask;
    }
}
