using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyProject.Application.Features.FinancialTransactions;

namespace MyProject.WebAPI.Controllers;

public sealed record AccountMoneyRequest(Guid AccountId, decimal Amount, string? Description);

public sealed record TransferMoneyRequest(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string? Description);

[ApiController]
[Route("api/[controller]")]
public sealed class FinancialTransactionsController(ISender sender) : ControllerBase
{
    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] AccountMoneyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DepositMoneyCommand(
            request.AccountId,
            request.Amount,
            idempotencyKey ?? string.Empty,
            request.Description), cancellationToken);
        return result.IsSuccess ? OperationStatus(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] AccountMoneyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new WithdrawMoneyCommand(
            request.AccountId,
            request.Amount,
            idempotencyKey ?? string.Empty,
            request.Description), cancellationToken);
        return result.IsSuccess ? OperationStatus(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] TransferMoneyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TransferMoneyCommand(
            request.SourceAccountId,
            request.DestinationAccountId,
            request.Amount,
            idempotencyKey ?? string.Empty,
            request.Description), cancellationToken);
        return result.IsSuccess ? OperationStatus(result.Value) : BadRequest(result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? accountId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? type = null,
        [FromQuery] decimal? minimumAmount = null,
        [FromQuery] decimal? maximumAmount = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool descending = true,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFinancialTransactionsQuery(
            accountId, fromUtc, toUtc, type, minimumAmount, maximumAmount,
            pageNumber, pageSize, descending);
        var result = await sender.Send(query, cancellationToken);
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
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? type = null,
        [FromQuery] decimal? minimumAmount = null,
        [FromQuery] decimal? maximumAmount = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool descending = true,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAccountTransactionHistoryQuery(
            accountId, fromUtc, toUtc, type, minimumAmount, maximumAmount,
            pageNumber, pageSize, descending);
        var result = await sender.Send(query, cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);
        return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
            ? NotFound(result.Error)
            : BadRequest(result.Error);
    }

    [HttpGet("reports")]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string period = "Daily",
        [FromQuery] Guid? accountId = null,
        CancellationToken cancellationToken = default)
    {
        if (fromUtc is null || toUtc is null)
            return BadRequest("fromUtc and toUtc are required and must be UTC timestamps.");

        var result = await sender.Send(
            new GetFinancialReportQuery(accountId, fromUtc.Value, toUtc.Value, period), cancellationToken);
        if (result.IsSuccess)
            return Ok(result.Value);
        return result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal)
            ? NotFound(result.Error)
            : BadRequest(result.Error);
    }

    private IActionResult OperationStatus(FinancialOperationResponse response)
    {
        if (response.Succeeded)
            return response.IsDuplicate ? Ok(response) : StatusCode(StatusCodes.Status201Created, response);
        if (response.FailureReason == "IdempotencyConflict")
            return Conflict(response);
        if (response.FailureReason == "AccountNotFound")
            return NotFound(response);
        return UnprocessableEntity(response);
    }
}
