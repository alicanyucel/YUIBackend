using Microsoft.EntityFrameworkCore;
using MyProject.Application.Abstractions.FinancialOperations;
using MyProject.Domain.Entities;
using MyProject.Infrastructure.Persistence;

namespace MyProject.Infrastructure.FinancialOperations;

internal sealed class FinancialOperationService(AppDbContext context) : IFinancialOperationService
{
    private const decimal MaximumBalance = 9_999_999_999_999_999.99m;

    public async Task<FinancialOperationExecution> ExecuteAsync(
        FinancialOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var existing = await context.FinancialTransactions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return Matches(existing, request)
                ? new FinancialOperationExecution(existing, existing.Status == FinancialTransactionStatus.Failed
                    ? GetFailureFor(existing) : FinancialOperationFailure.None, true)
                : new FinancialOperationExecution(null, FinancialOperationFailure.IdempotencyConflict, true);
        }

        var accountSnapshots = new Dictionary<Guid, AccountSnapshot>();
        foreach (var accountId in new[] { request.SourceAccountId, request.DestinationAccountId }
                     .Where(id => id.HasValue)
                     .Select(id => id!.Value)
                     .Distinct()
                     .Order())
        {
            var snapshot = await GetAccountSnapshotAsync(accountId, cancellationToken);
            if (snapshot is not null)
                accountSnapshots.Add(accountId, snapshot);
        }

        var source = request.SourceAccountId is Guid sourceId && accountSnapshots.TryGetValue(sourceId, out var sourceSnapshot)
            ? sourceSnapshot
            : null;
        var destination = request.DestinationAccountId is Guid destinationId &&
            accountSnapshots.TryGetValue(destinationId, out var destinationSnapshot)
            ? destinationSnapshot
            : null;

        if (request.SourceAccountId is not null && source is null ||
            request.DestinationAccountId is not null && destination is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new FinancialOperationExecution(null, FinancialOperationFailure.AccountNotFound, false);
        }

        var initialFailure = GetInitialFailure(request, source, destination);
        if (initialFailure != FinancialOperationFailure.None)
        {
            var failedExecution = await AddFailedTransactionAsync(request, initialFailure, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return failedExecution;
        }

        var updates = GetOrderedAccountUpdates(request);
        foreach (var update in updates)
        {
            var updated = update.IsCredit
                ? await CreditAsync(update.AccountId, request.Amount, cancellationToken)
                : await DebitAsync(update.AccountId, request.Amount, cancellationToken);

            if (!updated)
            {
                await transaction.RollbackAsync(cancellationToken);
                await transaction.DisposeAsync();
                return await RecordFailedOperationAsync(request, update, cancellationToken);
            }
        }

        var financialTransaction = CreateTransaction(request);
        financialTransaction.MarkSucceeded();
        context.FinancialTransactions.Add(financialTransaction);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new FinancialOperationExecution(financialTransaction, FinancialOperationFailure.None, false);
    }

    private async Task<AccountSnapshot?> GetAccountSnapshotAsync(Guid id, CancellationToken cancellationToken)
        => await context.Accounts
            .FromSqlInterpolated($"SELECT * FROM [dbo].[Accounts] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}")
            .AsNoTracking()
            .Where(account => account.Id == id)
            .Select(account => new AccountSnapshot(account.Id, account.Balance, account.CurrencyCode, account.Status))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> CreditAsync(Guid accountId, decimal amount, CancellationToken cancellationToken)
        => await context.Accounts
            .Where(account => account.Id == accountId && account.Status == AccountStatus.Active &&
                account.Balance <= MaximumBalance - amount)
            .ExecuteUpdateAsync(updates => updates.SetProperty(account => account.Balance,
                account => account.Balance + amount), cancellationToken) == 1;

    private async Task<bool> DebitAsync(Guid accountId, decimal amount, CancellationToken cancellationToken)
        => await context.Accounts
            .Where(account => account.Id == accountId && account.Status == AccountStatus.Active &&
                account.Balance >= amount)
            .ExecuteUpdateAsync(updates => updates.SetProperty(account => account.Balance,
                account => account.Balance - amount), cancellationToken) == 1;

    private async Task<FinancialOperationExecution> RecordFailedOperationAsync(
        FinancialOperationRequest request,
        AccountUpdate failedUpdate,
        CancellationToken cancellationToken)
    {
        await using var failureTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var existing = await context.FinancialTransactions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            await failureTransaction.CommitAsync(cancellationToken);
            return Matches(existing, request)
                ? new FinancialOperationExecution(existing, GetFailureFor(existing), true)
                : new FinancialOperationExecution(null, FinancialOperationFailure.IdempotencyConflict, true);
        }

        var current = await GetAccountSnapshotAsync(failedUpdate.AccountId, cancellationToken);
        var failure = current is null || current.Status != AccountStatus.Active
            ? FinancialOperationFailure.AccountInactive
            : failedUpdate.IsCredit && current.Balance > MaximumBalance - request.Amount
                ? FinancialOperationFailure.BalanceLimitExceeded
                : FinancialOperationFailure.InsufficientFunds;

        var result = await AddFailedTransactionAsync(request, failure, cancellationToken);
        await failureTransaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<FinancialOperationExecution> AddFailedTransactionAsync(
        FinancialOperationRequest request,
        FinancialOperationFailure failure,
        CancellationToken cancellationToken)
    {
        var financialTransaction = CreateTransaction(request);
        financialTransaction.MarkFailed(failure.ToString());
        context.FinancialTransactions.Add(financialTransaction);
        await context.SaveChangesAsync(cancellationToken);
        return new FinancialOperationExecution(financialTransaction, failure, false);
    }

    private static FinancialTransaction CreateTransaction(FinancialOperationRequest request)
    {
        var transactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";
        var financialTransaction = new FinancialTransaction(
            transactionNumber,
            request.Amount,
            request.Type,
            request.SourceAccountId,
            request.DestinationAccountId,
            request.IdempotencyKey);
        if (!string.IsNullOrWhiteSpace(request.Description))
            financialTransaction.SetDescription(request.Description.Trim());
        return financialTransaction;
    }

    private static FinancialOperationFailure GetInitialFailure(
        FinancialOperationRequest request,
        AccountSnapshot? source,
        AccountSnapshot? destination)
    {
        if (source is not null && source.Status != AccountStatus.Active ||
            destination is not null && destination.Status != AccountStatus.Active)
            return FinancialOperationFailure.AccountInactive;
        if (source is not null && destination is not null &&
            !string.Equals(source.CurrencyCode, destination.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            return FinancialOperationFailure.CurrencyMismatch;
        if (request.Type == FinancialTransactionType.Withdrawal && source!.Balance < request.Amount ||
            request.Type == FinancialTransactionType.Transfer && source!.Balance < request.Amount)
            return FinancialOperationFailure.InsufficientFunds;
        if ((request.Type == FinancialTransactionType.Deposit || request.Type == FinancialTransactionType.Transfer) &&
            destination!.Balance > MaximumBalance - request.Amount)
            return FinancialOperationFailure.BalanceLimitExceeded;
        return FinancialOperationFailure.None;
    }

    private static IReadOnlyList<AccountUpdate> GetOrderedAccountUpdates(FinancialOperationRequest request)
    {
        var updates = request.Type switch
        {
            var type when type == FinancialTransactionType.Deposit =>
                new[] { new AccountUpdate(request.DestinationAccountId!.Value, true) },
            var type when type == FinancialTransactionType.Withdrawal =>
                new[] { new AccountUpdate(request.SourceAccountId!.Value, false) },
            _ => new[]
            {
                new AccountUpdate(request.SourceAccountId!.Value, false),
                new AccountUpdate(request.DestinationAccountId!.Value, true)
            }
        };
        return updates.OrderBy(update => update.AccountId).ToArray();
    }

    private static bool Matches(FinancialTransaction transaction, FinancialOperationRequest request)
        => transaction.Type == request.Type && transaction.Amount == request.Amount &&
           transaction.SourceAccountId == request.SourceAccountId &&
           transaction.DestinationAccountId == request.DestinationAccountId &&
           transaction.Description == request.Description?.Trim();

    private static FinancialOperationFailure GetFailureFor(FinancialTransaction transaction)
        => Enum.TryParse<FinancialOperationFailure>(transaction.FailureReason, out var failure)
            ? failure
            : FinancialOperationFailure.InsufficientFunds;

    private sealed record AccountSnapshot(Guid Id, decimal Balance, string CurrencyCode, AccountStatus Status);
    private sealed record AccountUpdate(Guid AccountId, bool IsCredit);
}
