using MassTransit;
using MyProject.Application.Abstractions.Messaging;

namespace MyProject.Infrastructure.Messaging;

internal sealed class EventBus(IPublishEndpoint publishEndpoint) : IEventBus
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        => publishEndpoint.Publish(message, cancellationToken);

    public Task PublishAsync(object message, Type messageType, CancellationToken cancellationToken = default)
        => publishEndpoint.Publish(message, messageType, cancellationToken);
}
