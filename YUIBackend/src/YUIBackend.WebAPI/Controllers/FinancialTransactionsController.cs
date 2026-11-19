using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyProject.Application.Features.FinancialTransactions;

namespace MyProject.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class FinancialTransactionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetFinancialTransactionsQuery(pageNumber, pageSize), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetFinancialTransactionByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpGet("account/{accountId:guid}")]
    public async Task<IActionResult> GetAccountHistory(
        Guid accountId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetAccountTransactionHistoryQuery(accountId, pageNumber, pageSize), cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);
        return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
            ? NotFound(result.Error)
            : BadRequest(result.Error);
    }
}
