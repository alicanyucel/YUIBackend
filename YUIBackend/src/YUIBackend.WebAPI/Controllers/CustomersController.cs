using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyProject.Application.Features.Customers;
using MyProject.WebAPI.Authorization;

namespace MyProject.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CustomersController(ISender sender, ICustomerResourceAccess resourceAccess) : ControllerBase
{
    /// <summary>Müşterileri sayfalı listeler; müşteri kullanıcıları yalnızca kendi profilini görür.</summary>
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

        var result = await sender.Send(new GetCustomersQuery(pageNumber, pageSize, customerId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    /// <summary>Belirtilen müşterinin profil bilgilerini getirir.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!await resourceAccess.CanAccessCustomerAsync(User, id, cancellationToken))
            return NotFound();

        var result = await sender.Send(new GetCustomerByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    /// <summary>Yeni bir müşteri oluşturur. Yalnızca yöneticiler kullanabilir.</summary>
    [Authorize(Policy = "Administrator")]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : BadRequest(result.Error);
    }

    /// <summary>Müşteri profilini günceller; müşteri kendi aktiflik durumunu değiştiremez.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
            return BadRequest("Route id must match the request id.");

        var isAdmin = await resourceAccess.IsAdministratorAsync(User, cancellationToken);
        if (!isAdmin && !await resourceAccess.CanAccessCustomerAsync(User, id, cancellationToken))
            return NotFound();
        if (!isAdmin)
            command = command with { IsActive = null };

        var result = await sender.Send(command, cancellationToken);
        if (result.IsSuccess)
            return NoContent();
        return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
            ? NotFound(result.Error)
            : BadRequest(result.Error);
    }

    /// <summary>Müşteri profilini siler. Yalnızca yöneticiler kullanabilir.</summary>
    [Authorize(Policy = "Administrator")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteCustomerCommand(id), cancellationToken);
        if (result.IsSuccess)
            return NoContent();
        return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
            ? NotFound(result.Error)
            : BadRequest(result.Error);
    }
}
