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
                "MyProject.Infrastructure.FinancialOperations",
                "MyProject.Infrastructure.Cache",
                "MyProject.Infrastructure.MultiTenancy")
                .Where(type => !type.IsNested), publicOnly: false)
                    .AsImplementedInterfaces()
                    .WithScopedLifetime());
            services.AddScoped<IEventBus, EventBus>();
            services.AddScoped<Func<IEventBus>>(serviceProvider => serviceProvider.GetRequiredService<IEventBus>);

            services.AddHttpContextAccessor();
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(configuration.GetConnectionString("Redis")!);
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectRetry = Math.Min(redisOptions.ConnectRetry, 1);
            redisOptions.ConnectTimeout = Math.Min(redisOptions.ConnectTimeout, 1_000);
            redisOptions.AsyncTimeout = Math.Min(redisOptions.AsyncTimeout, 500);
            return ConnectionMultiplexer.Connect(redisOptions);
        });

        // MassTransit + EF transactional outbox + RabbitMQ + Saga
        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<AppDbContext>(options =>
            {
                options.UseSqlServer();
                options.UseBusOutbox();
            });

            x.AddConsumer<ProductCreatedConsumer>();
            x.AddConsumer<FinancialTransactionNotificationConsumer>();

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

                cfg.ReceiveEndpoint("financial-transaction-notifications", e =>
                {
                    e.UseEntityFrameworkOutbox<AppDbContext>(ctx);
                    e.ConfigureConsumer<FinancialTransactionNotificationConsumer>(ctx);
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
