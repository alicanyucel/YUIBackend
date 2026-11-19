using System.Security.Claims;

namespace MyProject.WebAPI.Authorization;

public interface ICustomerResourceAccess
{
    Task<bool> IsAdministratorAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<Guid?> GetCustomerIdAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task<bool> CanAccessCustomerAsync(ClaimsPrincipal principal, Guid customerId, CancellationToken cancellationToken);
    Task<bool> CanAccessAccountAsync(ClaimsPrincipal principal, Guid accountId, CancellationToken cancellationToken);
    Task<bool> CanAccessTransactionAsync(ClaimsPrincipal principal, Guid transactionId, CancellationToken cancellationToken);
}
