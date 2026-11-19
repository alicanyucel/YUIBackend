using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyProject.Application;
using MyProject.Application.Abstractions.FinancialOperations;
using MyProject.Infrastructure;

namespace MyProject.Tests.Infrastructure;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_ResolvesFinancialOperationServiceWithoutRegisteringNestedRecords()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = "Server=localhost;Database=YUIBackend;User Id=test;Password=TestOnly!Passw0rd42;TrustServerCertificate=True",
                ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false",
                ["ConnectionStrings:RabbitMQ"] = "rabbitmq://localhost"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        provider.Should().NotBeNull();
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IFinancialOperationService));
        services.Should().NotContain(descriptor =>
            descriptor.ServiceType.IsGenericType &&
            descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IEquatable<>));
    }
}
