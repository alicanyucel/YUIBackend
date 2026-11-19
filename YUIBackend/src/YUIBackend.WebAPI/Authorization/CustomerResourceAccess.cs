using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MyProject.Domain.Entities;
using MyProject.Infrastructure.Persistence;

namespace MyProject.WebAPI.Authorization;

public sealed class CustomerResourceAccess(AppDbContext dbContext) : ICustomerResourceAccess
{
    public async Task<bool> IsAdministratorAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
        => await GetUserAsync(principal, cancellationToken) is { IsAdmin: true };

    public async Task<Guid?> GetCustomerIdAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(principal, cancellationToken);
        if (user is not { IsAdmin: false, CustomerId: Guid customerId })
            return null;

        return await dbContext.Customers.AnyAsync(customer =>
            customer.Id == customerId && customer.IsActive, cancellationToken)
            ? customerId
            : null;
    }

    public async Task<bool> CanAccessCustomerAsync(
        ClaimsPrincipal principal,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(principal, cancellationToken);
        if (user is null)
            return false;
        if (user.IsAdmin)
            return true;
        return user.CustomerId == customerId &&
            await dbContext.Customers.AnyAsync(customer => customer.Id == customerId && customer.IsActive,
                cancellationToken);
    }

    public async Task<bool> CanAccessAccountAsync(
        ClaimsPrincipal principal,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(principal, cancellationToken);
        if (user is null)
            return false;
        if (user.IsAdmin)
            return true;
        return user.CustomerId is Guid customerId &&
            await dbContext.Accounts.AnyAsync(account => account.Id == accountId &&
                account.CustomerId == customerId && account.Customer.IsActive, cancellationToken);
    }

    public async Task<bool> CanAccessTransactionAsync(
        ClaimsPrincipal principal,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(principal, cancellationToken);
        if (user is null)
            return false;
        if (user.IsAdmin)
            return true;
        if (user.CustomerId is not Guid customerId ||
            !await dbContext.Customers.AnyAsync(customer => customer.Id == customerId && customer.IsActive,
                cancellationToken))
            return false;

        return await dbContext.FinancialTransactions.AnyAsync(transaction =>
            transaction.Id == transactionId &&
            ((transaction.SourceAccountId != null && transaction.SourceAccount!.CustomerId == customerId &&
                transaction.SourceAccount.Customer.IsActive) ||
             (transaction.DestinationAccountId != null && transaction.DestinationAccount!.CustomerId == customerId &&
                transaction.DestinationAccount.Customer.IsActive)),
            cancellationToken);
    }

    private async Task<InitialUser?> GetUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
            return null;

        return await dbContext.InitialUsers.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == userId && user.IsActive, cancellationToken);
    }
}
