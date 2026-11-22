using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyProject.Application.Features.Accounts;
using MyProject.WebAPI.Authorization;
using YUIBackend.Application.Features.Accounts.UpdateAccount;

namespace MyProject.WebAPI.Controllers;

public sealed record CreateAccountRequest(
    Guid CustomerId,
    string AccountNumber,
    string CurrencyCode,
    [param: Required] decimal? Balance);

[ApiController]
[Route("api/[controller]")]
public sealed class AccountsController(ISender sender, ICustomerResourceAccess resourceAccess) : ControllerBase
{
    /// <summary>Hesapları sayfalı olarak listeler; müşteri yalnızca kendi hesaplarını görür.</summary>
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

    /// <summary>Hesap bilgilerini ve mevcut bakiyesini getirir.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!await resourceAccess.CanAccessAccountAsync(User, id, cancellationToken))
            return NotFound();

        var result = await sender.Send(new GetAccountByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    /// <summary>Yeni bir hesap açar; başlangıç bakiyesinin istekte açıkça belirtilmesi gerekir.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Balance is not decimal balance)
            return BadRequest("Balance is required.");

        var command = new CreateAccountCommand(
            request.CustomerId,
            request.AccountNumber,
            request.CurrencyCode,
            balance);
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
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : BadRequest(result.Error);
    }

    /// <summary>Hesap durumunu günceller.</summary>
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

    /// <summary>Hesabı siler; silme işlemi için bakiyenin sıfır olması gerekir.</summary>
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
