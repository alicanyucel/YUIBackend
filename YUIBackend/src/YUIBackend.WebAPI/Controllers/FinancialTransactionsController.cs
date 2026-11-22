using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyProject.Application.Features.FinancialTransactions;
using MyProject.WebAPI.Authorization;
using YUIBackend.Application.Features.FinancialTransactions.FinancialOperations;

namespace MyProject.WebAPI.Controllers;

public sealed record AccountMoneyRequest(Guid AccountId, decimal Amount, string? Description);

public sealed record TransferMoneyRequest(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string? Description);

[ApiController]
[Route("api/[controller]")]
public sealed class FinancialTransactionsController(ISender sender, ICustomerResourceAccess resourceAccess) : ControllerBase
{
    /// <summary>Belirtilen hesaba para yatırır; yinelenen işlemleri önlemek için Idempotency-Key gönderilir.</summary>
    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] AccountMoneyRequest request,
        CancellationToken cancellationToken)
    {
        if (!await resourceAccess.CanAccessAccountAsync(User, request.AccountId, cancellationToken))
            return NotFound();

        var result = await sender.Send(new DepositMoneyCommand(
            request.AccountId,
            request.Amount,
            idempotencyKey ?? string.Empty,
            request.Description), cancellationToken);
        return result.IsSuccess ? OperationStatus(result.Value) : BadRequest(result.Error);
    }

    /// <summary>Belirtilen hesaptan para çeker; yinelenen işlemleri önlemek için Idempotency-Key gönderilir.</summary>
    [HttpPost("withdraw")]
    public async Task<IActionResult> Withdraw(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] AccountMoneyRequest request,
        CancellationToken cancellationToken)
    {
        if (!await resourceAccess.CanAccessAccountAsync(User, request.AccountId, cancellationToken))
            return NotFound();

        var result = await sender.Send(new WithdrawMoneyCommand(
            request.AccountId,
            request.Amount,
            idempotencyKey ?? string.Empty,
            request.Description), cancellationToken);
        return result.IsSuccess ? OperationStatus(result.Value) : BadRequest(result.Error);
    }

    /// <summary>Bir hesaptan diğerine para aktarır; yinelenen işlemleri önlemek için Idempotency-Key gönderilir.</summary>
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] TransferMoneyRequest request,
        CancellationToken cancellationToken)
    {
        if (!await resourceAccess.CanAccessAccountAsync(User, request.SourceAccountId, cancellationToken))
            return NotFound();

        var result = await sender.Send(new TransferMoneyCommand(
            request.SourceAccountId,
            request.DestinationAccountId,
            request.Amount,
            idempotencyKey ?? string.Empty,
            request.Description), cancellationToken);
        return result.IsSuccess ? OperationStatus(result.Value) : BadRequest(result.Error);
    }

    /// <summary>Finansal işlemleri filtrelerle arar ve sayfalı olarak listeler.</summary>
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
        var isAdmin = await resourceAccess.IsAdministratorAsync(User, cancellationToken);
        var customerId = isAdmin ? null : await resourceAccess.GetCustomerIdAsync(User, cancellationToken);
        if (!isAdmin && customerId is null)
            return Forbid();
        if (accountId is Guid requestedAccountId &&
            !await resourceAccess.CanAccessAccountAsync(User, requestedAccountId, cancellationToken))
            return NotFound();

        var query = new GetFinancialTransactionsQuery(
            accountId, fromUtc, toUtc, type, minimumAmount, maximumAmount,
            pageNumber, pageSize, descending, customerId);
        var result = await sender.Send(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    /// <summary>Belirtilen finansal işlemin ayrıntılarını getirir.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!await resourceAccess.CanAccessTransactionAsync(User, id, cancellationToken))
            return NotFound();

        var result = await sender.Send(new GetFinancialTransactionByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    /// <summary>Belirtilen hesaba ait işlem geçmişini filtreler ve sayfalı olarak listeler.</summary>
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
        if (!await resourceAccess.CanAccessAccountAsync(User, accountId, cancellationToken))
            return NotFound();

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

    /// <summary>Seçilen tarih aralığı ve döneme göre finansal işlem raporu getirir.</summary>
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

        var isAdmin = await resourceAccess.IsAdministratorAsync(User, cancellationToken);
        var customerId = isAdmin ? null : await resourceAccess.GetCustomerIdAsync(User, cancellationToken);
        if (!isAdmin && customerId is null)
            return Forbid();
        if (accountId is Guid requestedAccountId &&
            !await resourceAccess.CanAccessAccountAsync(User, requestedAccountId, cancellationToken))
            return NotFound();

        var result = await sender.Send(
            new GetFinancialReportQuery(accountId, fromUtc.Value, toUtc.Value, period, customerId), cancellationToken);
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
