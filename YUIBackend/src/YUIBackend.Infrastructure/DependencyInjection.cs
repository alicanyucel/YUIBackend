using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyProject.Application.Abstractions.Messaging;
using MyProject.Domain.Abstractions;
using MyProject.Infrastructure.Messaging;
using MyProject.Infrastructure.Messaging.Consumers;
using MyProject.Infrastructure.Messaging.Sagas;
using MyProject.Infrastructure.Persistence;
using Scrutor;
using StackExchange.Redis;

namespace MyProject.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // EF Core
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("SqlServer")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        // Automatically register infrastructure implementations by their interfaces.
        services.Scan(scan => scan
            .FromAssemblyOf<AppDbContext>()
            .AddClasses(classes => classes.InNamespaces(
                "MyProject.Infrastructure.Repositories",
                "MyProject.Infrastructure.Cache",
                "MyProject.Infrastructure.MultiTenancy"), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());
        services.AddScoped<IEventBus, EventBus>();

        services.AddHttpContextAccessor();
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));

        // MassTransit + EF transactional outbox + RabbitMQ + Saga
        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<AppDbContext>(options =>
            {
                options.UseSqlServer();
                options.UseBusOutbox();
            });

            x.AddConsumer<ProductCreatedConsumer>();

            x.AddSagaStateMachine<OrderStateMachine, OrderState>()
                .InMemoryRepository();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(configuration.GetConnectionString("RabbitMQ"));

                cfg.ReceiveEndpoint("product-created", e =>
                {
                    e.UseEntityFrameworkOutbox<AppDbContext>(ctx);
                    e.ConfigureConsumer<ProductCreatedConsumer>(ctx);
                });

                cfg.ReceiveEndpoint("order-saga", e =>
                {
                    e.UseEntityFrameworkOutbox<AppDbContext>(ctx);
                    e.ConfigureSaga<OrderState>(ctx);
                });
            });
        });

        return services;
    }
}
