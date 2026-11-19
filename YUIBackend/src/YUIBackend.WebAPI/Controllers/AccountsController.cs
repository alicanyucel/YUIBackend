using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyProject.Application.Features.Accounts;
using MyProject.WebAPI.Authorization;

namespace MyProject.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AccountsController(ISender sender, ICustomerResourceAccess resourceAccess) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = await resourceAccess.IsAdministratorAsync(User, cancellationToken);
        var customerId = isAdmin ? null : await resourceAccess.GetCustomerIdAsync(User, cancellationToken);
        if (!isAdmin && customerId is null)
            return Forbid();

        var result = await sender.Send(new GetAccountsQuery(pageNumber, pageSize, customerId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!await resourceAccess.CanAccessAccountAsync(User, id, cancellationToken))
            return NotFound();

        var result = await sender.Send(new GetAccountByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAccountCommand command,
        CancellationToken cancellationToken)
    {
        var isAdmin = await resourceAccess.IsAdministratorAsync(User, cancellationToken);
        if (!isAdmin)
        {
            var customerId = await resourceAccess.GetCustomerIdAsync(User, cancellationToken);
            if (customerId is null)
                return Forbid();
            command = command with { CustomerId = customerId.Value };
        }

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAccountCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
            return BadRequest("Route id must match the request id.");
        if (!await resourceAccess.CanAccessAccountAsync(User, id, cancellationToken))
            return NotFound();

        var result = await sender.Send(command, cancellationToken);
        if (result.IsSuccess)
            return NoContent();
        return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
            ? NotFound(result.Error)
            : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!await resourceAccess.CanAccessAccountAsync(User, id, cancellationToken))
            return NotFound();

        var result = await sender.Send(new DeleteAccountCommand(id), cancellationToken);
        if (result.IsSuccess)
            return NoContent();
        return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
            ? NotFound(result.Error)
            : BadRequest(result.Error);
    }
}
