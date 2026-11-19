using System.Security.Claims;
using FluentAssertions;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyProject.Application;
using MyProject.Application.Abstractions.FinancialOperations;
using MyProject.Domain.Abstractions;
using MyProject.Domain.Entities;
using MyProject.Domain.Repositories;
using MyProject.Infrastructure;
using MyProject.Infrastructure.Persistence;
using MyProject.WebAPI.Authorization;

namespace MyProject.Tests.Integration;

[CollectionDefinition("SQL Server integration", DisableParallelization = true)]
public sealed class SqlServerIntegrationCollection : ICollectionFixture<SqlServerIntegrationFixture>
{
    public const string Name = "SQL Server integration";
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("YUIBACKEND_TEST_SQLSERVER")))
            Skip = "Set YUIBACKEND_TEST_SQLSERVER to a SQL Server connection string with permission to create and drop databases.";
    }
}

public sealed class SqlServerIntegrationFixture : IAsyncLifetime
{
    private readonly string? _adminConnectionString = Environment.GetEnvironmentVariable("YUIBACKEND_TEST_SQLSERVER");
    private string? _databaseName;
    private ServiceProvider? _serviceProvider;

    public IServiceProvider Services => _serviceProvider ??
        throw new InvalidOperationException("SQL Server integration tests are not configured.");

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_adminConnectionString))
            return;

        _databaseName = $"YUIBackend_Integration_{Guid.NewGuid():N}";
        var adminBuilder = new SqlConnectionStringBuilder(_adminConnectionString)
        {
            InitialCatalog = "master"
        };

        await using (var connection = new SqlConnection(adminBuilder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        var databaseBuilder = new SqlConnectionStringBuilder(_adminConnectionString)
        {
            InitialCatalog = _databaseName
        };
        ConnectionString = databaseBuilder.ConnectionString;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = ConnectionString,
                ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false",
                ["ConnectionStrings:RabbitMQ"] = "rabbitmq://localhost"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICustomerResourceAccess, CustomerResourceAccess>();
        _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        await using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task<Guid[]> CreateAccountsAsync(int accountCount)
    {
        if (accountCount < 1)
            throw new ArgumentOutOfRangeException(nameof(accountCount));

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new Customer("Integration", "Test", $"{Guid.NewGuid():N}@example.test");
        var accounts = Enumerable.Range(0, accountCount)
            .Select(_ => new Account(customer.Id, $"IT-{Guid.NewGuid():N}", "TRY"))
            .ToArray();

        dbContext.Customers.Add(customer);
        dbContext.Accounts.AddRange(accounts);
        await dbContext.SaveChangesAsync();
        return accounts.Select(account => account.Id).ToArray();
    }

    public async Task<(Guid CustomerId, Guid UserId, Guid AccountId)> CreateCustomerAccountUserAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var customer = new Customer("Integration", "User", $"{suffix}@example.test");
        var user = new InitialUser($"user-{suffix}", $"USER-{suffix}", "test-hash", customer.Id);
        var account = new Account(customer.Id, $"IT-{suffix}", "TRY");

        dbContext.Customers.Add(customer);
        dbContext.InitialUsers.Add(user);
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();
        return (customer.Id, user.Id, account.Id);
    }

    public async Task<FinancialOperationExecution> ExecuteAsync(FinancialOperationRequest request)
    {
        await using var scope = Services.CreateAsyncScope();
        var operationService = scope.ServiceProvider.GetRequiredService<IFinancialOperationService>();
        return await operationService.ExecuteAsync(request);
    }

    public async Task<decimal> GetBalanceAsync(Guid accountId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Accounts.AsNoTracking()
            .Where(account => account.Id == accountId)
            .Select(account => account.Balance)
            .SingleAsync();
    }

    public async Task<int> CountTransactionsAsync(string idempotencyKey)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.FinancialTransactions.AsNoTracking()
            .CountAsync(transaction => transaction.IdempotencyKey == idempotencyKey);
    }

    public async Task<PagedResult<FinancialTransaction>> GetHistoryAsync(
        FinancialTransactionFilter filter,
        int pageNumber,
        int pageSize)
    {
        await using var scope = Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFinancialTransactionRepository>();
        return await repository.GetFilteredPageAsync(filter, pageNumber, pageSize);
    }

    public async Task<FinancialReport> GetReportAsync(
        FinancialReportFilter filter,
        FinancialReportPeriodKind period)
    {
        await using var scope = Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFinancialTransactionRepository>();
        return await repository.GetReportAsync(filter, period);
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();

        if (_databaseName is null || string.IsNullOrWhiteSpace(_adminConnectionString))
            return;

        SqlConnection.ClearAllPools();
        var adminBuilder = new SqlConnectionStringBuilder(_adminConnectionString)
        {
            InitialCatalog = "master"
        };
        await using var connection = new SqlConnection(adminBuilder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]";
        await command.ExecuteNonQueryAsync();
    }
}

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class FinancialOperationSqlServerIntegrationTests(SqlServerIntegrationFixture fixture)
{
    [SqlServerFact]
    public async Task CustomerProfileUpdate_UpdatesDetailsWithoutChangingActivityStatus()
    {
        var customerData = await fixture.CreateCustomerAccountUserAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var newEmail = $"updated-{Guid.NewGuid():N}@example.test";

        var result = await sender.Send(new MyProject.Application.Features.Customers.UpdateCustomerCommand(
            customerData.CustomerId, "Updated", "Customer", newEmail, "+1 555 0100"));

        result.IsSuccess.Should().BeTrue();
        var customer = await dbContext.Customers.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == customerData.CustomerId);
        customer.FirstName.Should().Be("Updated");
        customer.Email.Should().Be(newEmail);
        customer.PhoneNumber.Should().Be("+1 555 0100");
        customer.IsActive.Should().BeTrue();
    }

    [SqlServerFact]
    public async Task CustomerUsers_CanOnlyAccessTheirOwnCustomerAndAccounts()
    {
        var first = await fixture.CreateCustomerAccountUserAsync();
        var second = await fixture.CreateCustomerAccountUserAsync();
        var firstPrincipal = CreatePrincipal(first.UserId);
        var secondPrincipal = CreatePrincipal(second.UserId);

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var access = scope.ServiceProvider.GetRequiredService<ICustomerResourceAccess>();

        (await access.GetCustomerIdAsync(firstPrincipal, default)).Should().Be(first.CustomerId);
        (await access.CanAccessCustomerAsync(firstPrincipal, first.CustomerId, default)).Should().BeTrue();
        (await access.CanAccessCustomerAsync(firstPrincipal, second.CustomerId, default)).Should().BeFalse();
        (await access.CanAccessAccountAsync(firstPrincipal, first.AccountId, default)).Should().BeTrue();
        (await access.CanAccessAccountAsync(firstPrincipal, second.AccountId, default)).Should().BeFalse();
        (await access.CanAccessAccountAsync(secondPrincipal, first.AccountId, default)).Should().BeFalse();

        var customer = await dbContext.Customers.SingleAsync(candidate => candidate.Id == first.CustomerId);
        customer.IsActive = false;
        await dbContext.SaveChangesAsync();

        (await access.GetCustomerIdAsync(firstPrincipal, default)).Should().BeNull();
        (await access.CanAccessAccountAsync(firstPrincipal, first.AccountId, default)).Should().BeFalse();
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId) => new(
        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "IntegrationTest"));

    [SqlServerFact]
    public async Task DepositRetryWithSameKey_ChangesBalanceOnlyOnce()
    {
        var accountId = (await fixture.CreateAccountsAsync(1))[0];
        var request = new FinancialOperationRequest(
            FinancialTransactionType.Deposit, 125.50m, null, accountId, "deposit-integration-1", "Integration deposit");

        var first = await fixture.ExecuteAsync(request);
        var retry = await fixture.ExecuteAsync(request);
        var conflictingRetry = await fixture.ExecuteAsync(request with { Amount = 200m });

        first.IsSuccessful.Should().BeTrue();
        retry.IsSuccessful.Should().BeTrue();
        retry.IsDuplicate.Should().BeTrue();
        retry.Transaction!.Id.Should().Be(first.Transaction!.Id);
        conflictingRetry.Failure.Should().Be(FinancialOperationFailure.IdempotencyConflict);
        (await fixture.GetBalanceAsync(accountId)).Should().Be(125.50m);
        (await fixture.CountTransactionsAsync(request.IdempotencyKey)).Should().Be(1);
    }

    [SqlServerFact]
    public async Task InsufficientFundsTransfer_LeavesBothBalancesUnchangedAndRecordsFailure()
    {
        var accountIds = await fixture.CreateAccountsAsync(2);
        var request = new FinancialOperationRequest(
            FinancialTransactionType.Transfer, 50m, accountIds[0], accountIds[1], "transfer-integration-failure", null);

        var result = await fixture.ExecuteAsync(request);

        result.Failure.Should().Be(FinancialOperationFailure.InsufficientFunds);
        (await fixture.GetBalanceAsync(accountIds[0])).Should().Be(0m);
        (await fixture.GetBalanceAsync(accountIds[1])).Should().Be(0m);
        (await fixture.CountTransactionsAsync(request.IdempotencyKey)).Should().Be(1);
    }

    [SqlServerFact]
    public async Task ConcurrentWithdrawals_CannotOverdrawAnAccount()
    {
        var accountId = (await fixture.CreateAccountsAsync(1))[0];
        var deposit = new FinancialOperationRequest(
            FinancialTransactionType.Deposit, 100m, null, accountId, "concurrency-funding", null);
        (await fixture.ExecuteAsync(deposit)).IsSuccessful.Should().BeTrue();

        var withdrawals = new[]
        {
            new FinancialOperationRequest(FinancialTransactionType.Withdrawal, 80m, accountId, null, "concurrent-withdrawal-1", null),
            new FinancialOperationRequest(FinancialTransactionType.Withdrawal, 80m, accountId, null, "concurrent-withdrawal-2", null)
        };

        var results = await Task.WhenAll(withdrawals.Select(fixture.ExecuteAsync));

        results.Count(result => result.IsSuccessful).Should().Be(1);
        results.Count(result => result.Failure == FinancialOperationFailure.InsufficientFunds).Should().Be(1);
        (await fixture.GetBalanceAsync(accountId)).Should().Be(20m);
    }

    [SqlServerFact]
    public async Task AccountHistoryFiltersAndDailyReport_ReturnDatabaseAggregates()
    {
        var accountId = (await fixture.CreateAccountsAsync(1))[0];
        var deposit = new FinancialOperationRequest(
            FinancialTransactionType.Deposit, 100m, null, accountId, "report-deposit", null);
        var withdrawal = new FinancialOperationRequest(
            FinancialTransactionType.Withdrawal, 30m, accountId, null, "report-withdrawal", null);
        (await fixture.ExecuteAsync(deposit)).IsSuccessful.Should().BeTrue();
        (await fixture.ExecuteAsync(withdrawal)).IsSuccessful.Should().BeTrue();

        var fromUtc = DateTime.UtcNow.Date.AddDays(-1);
        var toUtc = fromUtc.AddDays(3);
        var history = await fixture.GetHistoryAsync(
            new FinancialTransactionFilter(accountId, fromUtc, toUtc,
                FinancialTransactionType.Withdrawal, 30m, 30m),
            pageNumber: 1,
            pageSize: 10);
        var report = await fixture.GetReportAsync(
            new FinancialReportFilter(accountId, fromUtc, toUtc),
            FinancialReportPeriodKind.Daily);

        history.TotalCount.Should().Be(1);
        history.Items.Should().ContainSingle(transaction =>
            transaction.Type == FinancialTransactionType.Withdrawal && transaction.Amount == 30m);
        report.TotalMoneyIn.Should().Be(100m);
        report.TotalMoneyOut.Should().Be(30m);
        report.SucceededCount.Should().Be(2);
        report.FailedCount.Should().Be(0);
    }
}
